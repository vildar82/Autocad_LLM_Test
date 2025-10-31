## Каркас плагина AutoCAD MCP

- Проект `AutocadMcpPlugin` собирается как библиотека `.NET Framework 4.8` с поддержкой WPF.
- Команда `MCPCHAT` открывает WPF-палитру, построенную по MVVM (`ChatViewModel` + `ChatMessage`, команды из `CommunityToolkit.Mvvm`).
- DI-контейнер `Microsoft.Extensions.DependencyInjection` конфигурируется в `PluginServiceProvider`; зарегистрированы `AutocadCommandExecutor`, `ConversationCoordinator`, `OpenAiLlmClient`, `SettingsViewModel`, `ChatViewModel`, а также `JsonSettingsService`.
- Настройки (ключ OpenAI) сохраняются в `%AppData%\AutocadMcpPlugin\settings.json`; окно вызывается из палитры кнопкой «Настройки».
- Клиент OpenAI берёт ключ из настроек, выполняет запросы к `chat/completions` и возвращает результат в чат.
- Зависимости AutoCAD подключаются через пакет NuGet `AutoCAD2019.Base`.
- Сборка: `dotnet build Autocad_MCP_Test.sln`.
