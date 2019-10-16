﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using DXVisualTestFixer.Common;

namespace DXVisualTestFixer.Core {
	public class CorpDirTestInfoContainer {
		public CorpDirTestInfoContainer(List<CorpDirTestInfo> failedTests, List<string> usedFilesLinks, List<IElapsedTimeInfo> elapsedTimes, List<Team> teams, (DateTime sources, DateTime tests)? buildTime) {
			FailedTests = failedTests;
			UsedFilesLinks = usedFilesLinks;
			ElapsedTimes = elapsedTimes;
			Teams = teams;
			SourcesBuildTime = buildTime?.sources;
			TestsBuildTime = buildTime?.tests;
		}

		public List<CorpDirTestInfo> FailedTests { get; }
		public List<string> UsedFilesLinks { get; }
		public List<IElapsedTimeInfo> ElapsedTimes { get; }
		public List<Team> Teams { get; }
		public DateTime? SourcesBuildTime { get; }
		public DateTime? TestsBuildTime { get; }
	}

	internal class ElapsedTimeInfo : IElapsedTimeInfo {
		public ElapsedTimeInfo(string name, TimeSpan time) {
			Name = name;
			Time = time;
		}

		public string Name { get; }
		public TimeSpan Time { get; }
	}

	public static class TestLoader {
		public static CorpDirTestInfoContainer LoadFromInfo(IFarmTaskInfo taskInfo, string realUrl) {
			var failedTests = new List<CorpDirTestInfo>();
			if(realUrl == null || !realUrl.Contains("ViewBuildReport.aspx")) throw new NotSupportedException("Contact Petr Zinovyev, please.");
			var myXmlDocument = LoadFromUrl(realUrl);
			var failedTestsTasks = new List<Task<List<CorpDirTestInfo>>>();
			if(!IsSuccessBuild(myXmlDocument)) {
				foreach(var testCaseXml in FindFailedTests(myXmlDocument)) {
					var testNameAndNamespace = testCaseXml.GetAttribute("name");
					var failureNode = testCaseXml.FindByName("failure");
					failedTestsTasks.Add(Task.Factory.StartNew(() => {
						var resultNode = failureNode.FindByName("message");
						var stackTraceNode = failureNode.FindByName("stack-trace");
						var localRes = new List<CorpDirTestInfo>();
						ParseMessage(taskInfo, testNameAndNamespace, resultNode.InnerText, stackTraceNode.InnerText, localRes);
						return localRes;
					}));
				}

				var errors = FindErrors(myXmlDocument).ToList();
				if(errors.Count > 0)
					failedTestsTasks.Add(Task.Factory.StartNew(() => {
						var result = new List<CorpDirTestInfo>();
						foreach(var error in errors)
							result.Add(CorpDirTestInfo.CreateError(taskInfo, "Error", error.InnerText, null));
						return result;
					}));
				if(failedTestsTasks.Count > 0) {
					Task.WaitAll(failedTestsTasks.ToArray());
					failedTestsTasks.ForEach(t => failedTests.AddRange(t.Result));
				}
				else {
					if(!taskInfo.Success)
						failedTests.Add(CorpDirTestInfo.CreateError(taskInfo, "BuildError", "BuildError", "BuildError"));
				}
			}

			return new CorpDirTestInfoContainer(failedTests, FindUsedFilesLinks(myXmlDocument).ToList(), FindElapsedTimes(myXmlDocument), FindTeams(taskInfo.Repository.Version, myXmlDocument), FindTimings(myXmlDocument));
		}

		static XmlDocument LoadFromUrl(string realUrl) {
			var myXmlDocument = new XmlDocument();
			realUrl = realUrl.Replace("ViewBuildReport.aspx", "XmlBuildLog.xml");
			var i = 0;
			while(i++ < 10)
				try {
					myXmlDocument.Load(realUrl);
					return myXmlDocument;
				}
				catch {
					if(i == 10)
						throw;
				}

			throw new NotSupportedException();
		}

		static bool IsSuccessBuild(XmlDocument myXmlDocument) {
			var buildNode = FindBuildNode(myXmlDocument);
			if(buildNode == null)
				return false;
			return !buildNode.TryGetAttibute("error", out string _);
		}

		static (DateTime sources, DateTime tests)? FindTimings(XmlDocument myXmlDocument) {
			var buildNode = FindBuildNode(myXmlDocument);
			if(buildNode == null)
				return null;
			var timingsNode = buildNode.FindByName("Timings");
			if(timingsNode == null)
				return null;
			try {
				return (GetDateTimeFromTimingsString(timingsNode.FindByName("Sources").InnerText), GetDateTimeFromTimingsString(timingsNode.FindByName("Tests").InnerText));
			}
			catch {
				return null;
			}
		}

		static DateTime GetDateTimeFromTimingsString(string dateStr) {
			var dateAndTime = dateStr.Split('_');
			var dateSplit = dateAndTime[0].Split('-');
			var timeSplit = dateAndTime[1].Split('-');
			return new DateTime(Convert.ToInt32(dateSplit[0]), Convert.ToInt32(dateSplit[1]), Convert.ToInt32(dateSplit[2]), Convert.ToInt32(timeSplit[0]), Convert.ToInt32(timeSplit[1]), 0);
		}

		static IEnumerable<XmlElement> FindFailedTests(XmlDocument myXmlDocument) {
			var buildNode = FindBuildNode(myXmlDocument);
			if(buildNode == null)
				yield break;
			foreach(var testResults in buildNode.FindAllByName("test-results"))
			foreach(var subNode in FindAllFailedTests(testResults))
				yield return subNode;
		}

		static IEnumerable<XmlElement> FindErrors(XmlDocument myXmlDocument) {
			var buildNode = FindBuildNode(myXmlDocument);
			if(buildNode == null)
				yield break;
			foreach(var root in buildNode.FindAllByName("root"))
			foreach(XmlElement error in root.FindAllByName("error"))
				yield return error;
		}

		static IEnumerable<string> FindUsedFilesLinks(XmlDocument myXmlDocument) {
			var buildNode = FindBuildNode(myXmlDocument);
			if(buildNode == null)
				yield break;
			foreach(var usedFilesNode in buildNode.FindAllByName("FileUsingLogLink")) yield return usedFilesNode.InnerText.Replace("\n", string.Empty);
		}

		static List<IElapsedTimeInfo> FindElapsedTimes(XmlDocument myXmlDocument) {
			var result = new List<IElapsedTimeInfo>();
			var buildNode = FindBuildNode(myXmlDocument);
			if(buildNode == null)
				return result;
			foreach(var elapsedTimeNode in buildNode.FindAllByName("ElapsedTime")) {
				string name;
				if(!elapsedTimeNode.TryGetAttibute("Name", out name))
					continue;
				string time;
				if(!elapsedTimeNode.TryGetAttibute("Time", out time))
					continue;
				if(int.TryParse(time.Split('.').FirstOrDefault() ?? time, out var sec))
					result.Add(new ElapsedTimeInfo(name, TimeSpan.FromSeconds(sec)));
			}

			return result;
		}

		static List<Team> FindTeams(string version, XmlDocument myXmlDocument) {
			var result = new Dictionary<string, Team>();
			var buildNode = FindBuildNode(myXmlDocument);
			if(buildNode == null)
				return null;
			foreach(var teamNode in buildNode.FindAllByName("Project")) {
				int dpi;
				if(!teamNode.TryGetAttibute("Dpi", out dpi))
					continue;
				string teamName;
				if(!teamNode.TryGetAttibute("IncludedCategories", out teamName))
					continue;
				string resourcesFolder;
				if(!teamNode.TryGetAttibute("ResourcesFolder", out resourcesFolder))
					continue;
				string testResourcesPath;
				if(!teamNode.TryGetAttibute("TestResourcesPath", out testResourcesPath))
					continue;
				testResourcesPath = Path.Combine(resourcesFolder, testResourcesPath);
				string testResourcesPath_optimized = null;
				teamNode.TryGetAttibute("TestResourcesPath_Optimized", out testResourcesPath_optimized);
				if(testResourcesPath_optimized != null)
					testResourcesPath_optimized = Path.Combine(resourcesFolder, testResourcesPath_optimized);
				var projectInfosNode = teamNode.FindByName("ProjectInfos");
				if(projectInfosNode == null)
					continue;
				foreach(var projectInfoNode in projectInfosNode.FindAllByName("ProjectInfo")) {
					string serverFolderName;
					if(!projectInfoNode.TryGetAttibute("ServerFolderName", out serverFolderName))
						continue;
					bool optimized;
					projectInfoNode.TryGetAttibute("Optimized", out optimized);
					Team team;
					if(!result.TryGetValue(teamName, out team))
						result[teamName] = team = new Team {Name = teamName, Version = version};
					team.TeamInfos.Add(new TeamInfo {Dpi = dpi, Optimized = optimized, ServerFolderName = serverFolderName, TestResourcesPath = testResourcesPath, TestResourcesPath_Optimized = testResourcesPath_optimized});
				}
			}

			return result.Values.Count == 0 ? null : result.Values.ToList();
		}

		static bool TryGetAttibute<T>(this XmlNode node, string name, out T value) {
			value = default;
			var res = node.Attributes[name];
			if(res == null)
				return false;
			//value = (T)Convert.ChangeType(res, typeof(T));
			var converter = TypeDescriptor.GetConverter(typeof(T));
			value = (T) converter.ConvertFrom(res.Value);
			return true;
		}

		static XmlNode FindBuildNode(XmlDocument myXmlDocument) {
			return myXmlDocument.FindByName("cruisecontrol")?.FindByName("build");
		}

		static IEnumerable<XmlElement> FindAllFailedTests(XmlNode testResults) {
			foreach(XmlNode node in testResults.ChildNodes) {
				var xmlElement = node as XmlElement;
				if(xmlElement != null && xmlElement.Name == "test-case" && xmlElement.GetAttribute("success") == "False")
					yield return xmlElement;
				else
					foreach(var subNode in FindAllFailedTests(node))
						yield return subNode;
			}
		}

		static XmlNode FindByName(this XmlNode element, string name) {
			foreach(XmlNode node in element.ChildNodes)
				if(node.Name == name)
					return node;
			return null;
		}

		static IEnumerable<XmlNode> FindAllByName(this XmlNode element, string name) {
			foreach(XmlNode node in element.ChildNodes)
				if(node.Name == name)
					yield return node;
		}

		public static void ParseMessage(IFarmTaskInfo farmTaskInfo, string testNameAndNamespace, string message, string stackTrace, List<CorpDirTestInfo> resultList) {
			if(!message.StartsWith("Exception - NUnit.Framework.AssertionException")) {
				resultList.Add(CorpDirTestInfo.CreateError(farmTaskInfo, testNameAndNamespace, message, stackTrace));
				return;
			}

			var themedResultPaths = message.Split(new[] {" - failed:"}, StringSplitOptions.RemoveEmptyEntries).ToList();
			if(themedResultPaths.Count == 1) {
				resultList.Add(CorpDirTestInfo.CreateError(farmTaskInfo, testNameAndNamespace, message, stackTrace));
				return;
			}

			foreach(var part in themedResultPaths) ParseMessagePart(farmTaskInfo, testNameAndNamespace, part, resultList);
		}

		static void ParseMessagePart(IFarmTaskInfo farmTaskInfo, string testNameAndNamespace, string message, List<CorpDirTestInfo> resultList) {
			var parts = message.Split(new[] {"\n"}, StringSplitOptions.RemoveEmptyEntries).ToList();
			var paths = parts.Where(x => x.Contains(@"\\corp")).Select(x => x.Replace(@"\\corp", string.Empty)).ToList();

			var resultPaths = PatchPaths(paths);
			var shaList = PatchSHA(parts.Where(x => x.Contains("sha-")).Select(x => x.Replace("sha-", string.Empty)).ToList());
			var diffCount = TryGetDiffCount(parts.Where(x => x.Contains("diffCount=")).Select(x => x.Replace("diffCount=", string.Empty)).LastOrDefault());
			CorpDirTestInfo info = null;
			if(!CorpDirTestInfo.TryCreate(farmTaskInfo, testNameAndNamespace, resultPaths, shaList, diffCount, out info))
				return;
			resultList.Add(info);
		}

		static int? TryGetDiffCount(string str) {
			if(int.TryParse(str, out var res))
				return res;
			return null;
		}

		static List<string> PatchSHA(List<string> shaList) {
			var result = new List<string>();
			foreach(var sha in shaList) result.Add(sha.Replace("\r", string.Empty).Replace("\n", string.Empty));
			return result;
		}

		static List<string> PatchPaths(List<string> resultPaths) {
			var result = new List<string>();
			foreach(var pathCandidate in resultPaths) {
				if(!pathCandidate.Contains('\\'))
					continue;
				var cleanPath = @"\\corp" + pathCandidate.Replace("\r", string.Empty).Replace("\n", string.Empty).Replace(@"\\", @"\");
				if(cleanPath.Contains(' '))
					continue;
				if(File.Exists(cleanPath)) {
					result.Add(cleanPath);
					continue;
				}

				if(cleanPath.Contains("InstantBitmap.png.sha")) {
					SafeAddPath("InstantBitmap.png.sha", cleanPath, result);
					continue;
				}

				if(cleanPath.Contains("InstantBitmap.png")) {
					SafeAddPath("InstantBitmap.png", cleanPath, result);
					continue;
				}

				if(cleanPath.Contains("BitmapDif.png")) {
					SafeAddPath("BitmapDif.png", cleanPath, result);
					continue;
				}

				if(cleanPath.Contains("CurrentBitmap.png.sha")) {
					SafeAddPath("CurrentBitmap.png.sha", cleanPath, result);
					continue;
				}

				if(cleanPath.Contains("CurrentBitmap.png")) SafeAddPath("CurrentBitmap.png", cleanPath, result);
			}

			return result;
		}

		static void SafeAddPath(string fileName, string pathCandidate, List<string> paths) {
			var cleanPath = pathCandidate.Split(new[] {fileName}, StringSplitOptions.RemoveEmptyEntries).First() + fileName;
			if(File.Exists(cleanPath))
				paths.Add(cleanPath);
		}
	}
}