## Каркас плагина AutoCAD LLM

- Проект `AutocadLlmPlugin` собирается как библиотека `.NET Framework 4.8` с поддержкой WPF.
- Команда `LLMCHAT` открывает WPF-палитру, построенную по MVVM (`ChatViewModel` + `ChatMessage`, команды из `CommunityToolkit.Mvvm`).
- Настройки (ключ OpenAI) сохраняются через окно «Настройки» в `%AppData%\AutocadLlmPlugin\settings.json`.
- DI-контейнер `Microsoft.Extensions.DependencyInjection` конфигурируется в `PluginServiceProvider`; зарегистрированы `AutocadCommandExecutor`, `ConversationCoordinator`, `OpenAiLlmClient`, `SettingsViewModel`, `ChatViewModel`, а также `JsonSettingsService`.
- Клиент OpenAI использует настройки `OpenAiSettings`; методы описывают инструменты (круг, линия) и выполняют tool-calls для построения примитивов.
- Зависимости AutoCAD подключаются через пакет NuGet `AutoCAD2019.Base`.
- Сборка: `dotnet build Autocad_LLM_Test.sln`.