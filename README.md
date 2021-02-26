# DXVisualTestsFixer

## Деплой

1. Поднять версию в VersionInfo.VersionString.cs
2. Rebuild
3. Cкопировать в C:\Work\DXVisualTestFixer\packages\squirrel.windows.1.9.1\tools
    - [Nuget.exe](https://dist.nuget.org/win-x86-commandline/latest/nuget.exe)
    - эталонный DXVisualTestFixer\lib\Wpf\DXVisualTestFixer.3.6.28.nuspec
4. Rebuild DebugPublish
5. Поднимется заполненная форма -> Publish
6. Cкопировать **все** результаты из задеплоенной папки (C:\release\wpf) в \\\corp\internal\common\visualTests_squirrel

### Поднятие версии компонетов и паблишинг в Nuget
1. Find/Replace текущую версию на необходимую мажор+минор
    - DXVisualTestFixer\DXVcs2Git.Nuget\DXVisualTests_UI_Addin_Dependencies.nuspec
    - DXVisualTestFixer\DXVcs2Git.Nuget\generate-nuget-dependencies.py
2. Запустить `generate-nuget-dependencies.py --push`
3. Обновить версию пакета (DXVisualTests_UI_Addin_Dependencies) для DXVisualTestFixer.UI и DXVisualTestFixer (source http://nuget.devexpress.devx/nuget/)