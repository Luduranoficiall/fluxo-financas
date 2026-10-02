using Fluxo.Core;
using Fluxo.Core.Persistence;
using Microsoft.JSInterop;

namespace Fluxo.Web.Services;

/// <summary>
/// Liga o <see cref="Ledger"/> ao navegador: carrega e salva no localStorage e avisa a tela
/// quando algo muda. Nenhum dado sai do computador da pessoa.
/// </summary>
public sealed class LedgerService(IJSRuntime js)
{
    private const string StateKey = "fluxo.state.v1";
    private const string SampleKey = "fluxo.sample";

    public Ledger Ledger { get; private set; } = new();
    public bool Loaded { get; private set; }
    public bool IsSample { get; private set; }
    public DateOnly Month { get; private set; } = FirstOfMonth(DateOnly.FromDateTime(DateTime.Today));
    public string? StorageError { get; private set; }

    public event Action? Changed;

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public async Task LoadAsync()
    {
        if (Loaded) return;
        var json = await TryGet(StateKey);
        IsSample = await TryGet(SampleKey) == "1";
        Ledger = new Ledger(LedgerJson.Deserialize(json));
        Ledger.Changed += OnLedgerChanged;
        Ledger.SeedStarterRules();
        FocusLatestMonth();
        Loaded = true;
        Changed?.Invoke();
    }

    public void SetMonth(DateOnly month)
    {
        Month = FirstOfMonth(month);
        Changed?.Invoke();
    }

    /// <summary>Depois de importar, mostra o mês mais recente que tem lançamento.</summary>
    public void FocusLatestMonth()
    {
        if (Ledger.Transactions.Count > 0) Month = FirstOfMonth(Ledger.Transactions.Max(t => t.Date));
    }

    public async Task LoadSampleAsync()
    {
        Ledger.Reset();
        Ledger.SeedStarterRules();
        Ledger.Import(SampleData.Statement(Today), "exemplo");
        SampleData.ApplyBudgetsAndGoals(Ledger, Today);
        IsSample = true;
        await TrySet(SampleKey, "1");
        FocusLatestMonth();
        Changed?.Invoke();
    }

    public async Task ClearAsync()
    {
        Ledger.Reset();
        Ledger.SeedStarterRules();
        IsSample = false;
        await TrySet(SampleKey, "0");
        Month = FirstOfMonth(Today);
        Changed?.Invoke();
    }

    private void OnLedgerChanged()
    {
        _ = TrySet(StateKey, LedgerJson.Serialize(Ledger.State));
        Changed?.Invoke();
    }

    private async Task<string?> TryGet(string key)
    {
        try { return await js.InvokeAsync<string?>("localStorage.getItem", key); }
        catch (JSException) { StorageError = "O navegador bloqueou o armazenamento local. Os dados valem só enquanto a aba estiver aberta."; return null; }
    }

    private async Task TrySet(string key, string value)
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", key, value); StorageError = null; }
        catch (JSException) { StorageError = "Não consegui salvar no navegador (armazenamento cheio ou bloqueado)."; Changed?.Invoke(); }
    }

    private static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);
}
