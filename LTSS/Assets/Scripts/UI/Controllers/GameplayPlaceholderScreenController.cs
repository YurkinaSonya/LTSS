using System.Text;
using Game.Core.Application.UI;
using Game.Core.Application.Session;
using Game.Domain.GameFlow;

public sealed class GameplayPlaceholderScreenController : ScreenController
{
    private readonly GameplayPlaceholderScreenView _view;

    public GameplayPlaceholderScreenController(GameplayPlaceholderScreenView view, UIContext context)
        : base(view, context)
    {
        _view = view;
    }

    public override void Open()
    {
        base.Open();

        _view.BindBack(OnBack);
        ApplyDetails();

        if (Context.GameSession != null)
        {
            Context.GameSession.Changed += OnGameSessionChanged;
        }

        if (Context.SessionCoordinator != null)
        {
            Context.SessionCoordinator.RuntimeChanged += OnRuntimeChanged;
        }
    }

    public override void Dispose()
    {
        if (Context.GameSession != null)
        {
            Context.GameSession.Changed -= OnGameSessionChanged;
        }

        if (Context.SessionCoordinator != null)
        {
            Context.SessionCoordinator.RuntimeChanged -= OnRuntimeChanged;
        }

        _view.BindBack(null);
    }

    private void OnBack()
    {
        Context.Navigation?.ShowSessionReady("gameplay_placeholder_back");
    }

    private void OnGameSessionChanged(GameSessionSnapshot snapshot)
    {
        ApplyDetails();
    }

    private void OnRuntimeChanged(ClientRuntimeState runtimeState)
    {
        ApplyDetails();
    }

    private void ApplyDetails()
    {
        if (_view == null)
        {
            return;
        }

        var runtimeState = Context.SessionCoordinator != null
            ? Context.SessionCoordinator.CurrentRuntime
            : ClientRuntimeState.Empty;
        var gameSession = Context.GameSession != null
            ? Context.GameSession.Current
            : GameSessionSnapshot.Empty;
        var builder = new StringBuilder();

        builder.AppendLine("Технический экран");
        builder.AppendLine();
        builder.Append("Этап: ").AppendLine(gameSession.Stage.ToString());
        builder.Append("Фаза: ").AppendLine(gameSession.Phase.ToString());
        builder.Append("Метрики: ")
            .Append(gameSession.Metrics.NumericMetrics.Count)
            .Append(" числ., ")
            .Append(gameSession.Metrics.StringMetrics.Count)
            .AppendLine(" текст.");

        if (runtimeState != null && runtimeState.HasSession)
        {
            builder.AppendLine();
            builder.Append("Сессия: ")
                .AppendLine(string.IsNullOrWhiteSpace(runtimeState.Bootstrap.Session.Title)
                    ? runtimeState.Bootstrap.Session.Code
                    : runtimeState.Bootstrap.Session.Title);
            builder.Append("Запуск: ")
                .Append(runtimeState.Bootstrap.Run.RunId)
                .Append(" / ")
                .AppendLine(runtimeState.Bootstrap.Run.RunStatus.ToString());
            builder.Append("Период: ")
                .AppendLine(runtimeState.Bootstrap.Run.CurrentPeriodNumber.ToString());
        }

        _view.SetDetails(builder.ToString().TrimEnd());
    }
}
