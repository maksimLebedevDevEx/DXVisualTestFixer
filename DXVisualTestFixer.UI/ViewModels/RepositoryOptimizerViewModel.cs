﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using DevExpress.Mvvm;
using DevExpress.Xpf.Core;
using DXVisualTestFixer.Common;
using DXVisualTestFixer.UI.Models;
using JetBrains.Annotations;
using Prism.Interactivity.InteractionRequest;
using BindableBase = Prism.Mvvm.BindableBase;
using INotification = Prism.Interactivity.InteractionRequest.INotification;

namespace DXVisualTestFixer.UI.ViewModels {
	[UsedImplicitly]
	public class RepositoryOptimizerViewModel : BindableBase, IConfirmation {
		readonly Dispatcher _dispatcher;
		readonly IMinioWorker _minioWorker;
		readonly ITestsService _testsService;
		static readonly string _resourcesPathMask = "Images_";

		ObservableCollection<RepositoryFileModel> _removedFiles;
		ProgramStatus _status;
		ObservableCollection<RepositoryFileModel> _unusedFiles;

		public RepositoryOptimizerViewModel(ITestsService testsService, IMinioWorker minioWorker) {
			_dispatcher = Dispatcher.CurrentDispatcher;
			_minioWorker = minioWorker;
			_testsService = testsService;
			RemovedFiles = new ObservableCollection<RepositoryFileModel>();
			Commands = UICommand.GenerateFromMessageButton(MessageButton.OKCancel, new DialogService(), MessageResult.OK, MessageResult.Cancel);
			Commands.Single(c => c.IsDefault).Command = new DelegateCommand(Commit);
			Status = ProgramStatus.Loading;
			Task.Factory.StartNew(() => UpdateUnusedFiles(testsService.SelectedState.UsedFilesLinks)).ConfigureAwait(false);
		}

		[UsedImplicitly]
		public ObservableCollection<RepositoryFileModel> UnusedFiles {
			get => _unusedFiles;
			set => SetProperty(ref _unusedFiles, value);
		}

		[UsedImplicitly]
		public ObservableCollection<RepositoryFileModel> RemovedFiles {
			get => _removedFiles;
			set => SetProperty(ref _removedFiles, value);
		}

		[UsedImplicitly]
		public ProgramStatus Status {
			get => _status;
			set => SetProperty(ref _status, value);
		}

		[UsedImplicitly] public IEnumerable<UICommand> Commands { get; }

		[UsedImplicitly] public InteractionRequest<IConfirmation> ConfirmationRequest { get; } = new InteractionRequest<IConfirmation>();

		public bool Confirmed { get; set; }
		string INotification.Title { get; set; } = "Repository Optimizer";
		object INotification.Content { get; set; }

		void Commit() {
			foreach(var fileToRemove in RemovedFiles)
				if(File.Exists(fileToRemove.Path) && (File.GetAttributes(fileToRemove.Path) & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)
					File.Delete(fileToRemove.Path);
			Confirmed = true;
		}

		async Task UpdateUnusedFiles(Dictionary<Repository, List<string>> usedFilesByRepLinks) {
			var result = await Task.WhenAll(usedFilesByRepLinks.ToList().Select(x => GetUnusedFilesByRepository(x.Key, x.Value, _testsService, _minioWorker)));
			await _dispatcher.InvokeAsync(() => {
					UnusedFiles = new ObservableCollection<RepositoryFileModel>(result.SelectMany(x => x));
					Status = ProgramStatus.Idle;
				}
			);
		}

		public static async Task<HashSet<string>> GetUsedFilesByRepository(Repository repository, List<string> filesLinks, IMinioWorker minio) {
			using var archive = new ZipArchive(await minio.GetBinary(repository.MinioPath + "usedfiles.zip"), ZipArchiveMode.Read);
			var linksCache = new HashSet<string>(filesLinks);
			
			var usedFiles = new List<string>();
			
			foreach(var entry in archive.Entries) {
				if(!linksCache.Contains(entry.Name))
					continue;
				using var s = entry.Open();
				using var reader = new StreamReader(s);
				while(!reader.EndOfStream) {
					var fileRelPath = await reader.ReadLineAsync();
					var localFilePath = Path.Combine(repository.Path, fileRelPath.Replace(@"C:\builds\", ""));
					if(File.Exists(localFilePath))
						usedFiles.Add(localFilePath.ToLower());
				}
			}
			return new HashSet<string>(usedFiles);
		}

		static async Task<List<RepositoryFileModel>> GetUnusedFilesByRepository(Repository repository, List<string> usedFilesByRepLinks, ITestsService testsService, IMinioWorker minioWorker) {
			var usedFiles = await GetUsedFilesByRepository(repository, usedFilesByRepLinks, minioWorker).ConfigureAwait(false);
			var result = new List<RepositoryFileModel>();
			foreach(var resourceTopDirectory in Directory.EnumerateDirectories(repository.Path, _resourcesPathMask + "*", SearchOption.TopDirectoryOnly)) {
				foreach(var file in Directory.EnumerateFiles(resourceTopDirectory, "*", SearchOption.AllDirectories))
					if(!usedFiles.Contains(file.ToLower()))
						result.Add(new RepositoryFileModel(file, repository.Version));
			}
			return result;
		}
	}
}