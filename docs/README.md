## Каркас плагина AutoCAD MCP

- Проект `AutocadMcpPlugin` собран как библиотека `.NET Framework 4.7.2` с поддержкой WPF.
- Команда `MCPCHAT` открывает WPF-палитру с простым чат-интерфейсом.
- Используются реальные библиотеки AutoCAD (`AcCoreMgd.dll`, `AcDbMgd.dll`, `AcMgd.dll`) из каталога AutoCAD 2019. Путь можно переопределить свойством MSBuild `AutoCAD2019Root`.
- Для проверки сборки используйте `dotnet build Autocad_MCP_Test.sln`.
