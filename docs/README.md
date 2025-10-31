## Каркас плагина AutoCAD MCP

- Проект `AutocadMcpPlugin` собирается как библиотека `.NET Framework 4.8` с поддержкой WPF.
- Команда `MCPCHAT` открывает WPF-палитру, построенную по MVVM (`ChatViewModel` + `ChatMessage`, команды из `CommunityToolkit.Mvvm`).
- DI-контейнер `Microsoft.Extensions.DependencyInjection` конфигурируется в `PluginServiceProvider`; там же зарегистрированы `AutocadCommandExecutor`, `ConversationCoordinator`, `OpenAiLlmClient` и `ChatViewModel`.
- Клиент OpenAI использует настройки `OpenAiSettings`; по умолчанию ключ читается из переменной окружения `OPENAI_API_KEY`.
- Зависимости AutoCAD подключаются через пакет NuGet `AutoCAD2019.Base`.
- Сборка: `dotnet build Autocad_MCP_Test.sln`.
