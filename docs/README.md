## Каркас плагина AutoCAD MCP

- Проект `AutocadMcpPlugin` — библиотека `.NET Framework 4.8` с поддержкой WPF.
- Команда `MCPCHAT` открывает WPF-палитру с простым чат-интерфейсом. Ответ генерируется через заглушку `ConversationCoordinator`, подключённую к DI-контейнеру.
- Контейнер `Microsoft.Extensions.DependencyInjection` настраивается в `PluginServiceProvider` и будет расширяться по мере добавления сервисов MCP/LLM.
- Для проверки сборки используйте `dotnet build Autocad_MCP_Test.sln`.