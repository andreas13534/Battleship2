using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Purchasing;

/// <summary>
/// Thin Unity IAP 5 adapter. A store order is confirmed only after Cloud Code has
/// validated and granted it, so an interrupted validation is retried by the store.
/// </summary>
public sealed class NavalIapService : IDisposable
{
    public const string EliasProductId = "commander.elias.voss";
    public const string DaeProductId = "commander.dae.hyun.kwon";
    public const string ArjanProductId = "commander.arjan.dhillon";

    private static readonly List<ProductDefinition> Catalog = new List<ProductDefinition>
    {
        new ProductDefinition(EliasProductId, ProductType.NonConsumable),
        new ProductDefinition(DaeProductId, ProductType.NonConsumable),
        new ProductDefinition(ArjanProductId, ProductType.NonConsumable)
    };

    private readonly INavalOnlineService onlineService;
    private StoreController store;
    private bool initialized;
    private bool initializing;
    private readonly HashSet<string> validatingTransactions = new HashSet<string>();

    public event Action Changed;
    public bool IsReady { get; private set; }
    public string StatusMessage { get; private set; } = "STORE NICHT VERBUNDEN";

    public NavalIapService(INavalOnlineService onlineService)
    {
        this.onlineService = onlineService ?? throw new ArgumentNullException(nameof(onlineService));
    }

    public async Task InitializeAsync()
    {
        if (initialized || initializing) return;
        initializing = true;
        StatusMessage = "STORE WIRD VERBUNDEN...";
        RaiseChanged();
        try
        {
            store = UnityIAPServices.StoreController();
            store.OnStoreConnected += HandleStoreConnected;
            store.OnStoreDisconnected += HandleStoreDisconnected;
            store.OnProductsFetched += HandleProductsFetched;
            store.OnProductsFetchFailed += HandleProductsFetchFailed;
            store.OnPurchasePending += HandlePurchasePending;
            store.OnPurchaseConfirmed += HandlePurchaseConfirmed;
            store.OnPurchaseFailed += HandlePurchaseFailed;
            store.OnPurchaseDeferred += HandlePurchaseDeferred;
            await store.Connect();
            initialized = true;
        }
        catch (Exception exception)
        {
            StatusMessage = "STORE-FEHLER // " + SafeMessage(exception);
            RaiseChanged();
        }
        finally
        {
            initializing = false;
        }
    }

    public bool HasProduct(string productId)
        => store?.GetProducts()?.Any(product => product.definition.id == productId && product.availableToPurchase) == true;

    public string GetLocalizedPrice(string productId)
    {
        Product product = FindProduct(productId);
        return product?.metadata?.localizedPriceString ?? "—";
    }

    public void Purchase(string productId)
    {
        if (!onlineService.IsSignedIn) throw new InvalidOperationException("ONLINE-ANMELDUNG ERFORDERLICH");
        Product product = FindProduct(productId);
        if (!IsReady || product == null || !product.availableToPurchase)
            throw new InvalidOperationException("PRODUKT NICHT VERFÜGBAR");
        StatusMessage = "STORE-FREIGABE WIRD GEÖFFNET...";
        RaiseChanged();
        store.PurchaseProduct(product);
        ReplaceLegacyFakeStoreInputModule();
    }

    public void RestorePurchases()
    {
        if (!onlineService.IsSignedIn) throw new InvalidOperationException("ONLINE-ANMELDUNG ERFORDERLICH");
        if (store == null) throw new InvalidOperationException("STORE NICHT VERBUNDEN");
        StatusMessage = "KÄUFE WERDEN WIEDERHERGESTELLT...";
        RaiseChanged();
        store.RestoreTransactions((success, error) =>
        {
            StatusMessage = success ? "KÄUFE WIEDERHERGESTELLT" : "WIEDERHERSTELLUNG FEHLGESCHLAGEN // " + error;
            RaiseChanged();
        });
    }

    public void Dispose()
    {
        if (store == null) return;
        store.OnStoreConnected -= HandleStoreConnected;
        store.OnStoreDisconnected -= HandleStoreDisconnected;
        store.OnProductsFetched -= HandleProductsFetched;
        store.OnProductsFetchFailed -= HandleProductsFetchFailed;
        store.OnPurchasePending -= HandlePurchasePending;
        store.OnPurchaseConfirmed -= HandlePurchaseConfirmed;
        store.OnPurchaseFailed -= HandlePurchaseFailed;
        store.OnPurchaseDeferred -= HandlePurchaseDeferred;
    }

    private void HandleStoreConnected()
    {
        StatusMessage = "PRODUKTE WERDEN GELADEN...";
        RaiseChanged();
        store.FetchProducts(Catalog);
    }

    private void HandleStoreDisconnected(StoreConnectionFailureDescription failure)
    {
        IsReady = false;
        StatusMessage = "STORE OFFLINE // " + failure.message;
        RaiseChanged();
    }

    private void HandleProductsFetched(List<Product> products)
    {
        IsReady = products != null && products.Count > 0;
        StatusMessage = IsReady ? "STORE BEREIT" : "KEINE PRODUKTE VERFÜGBAR";
        RaiseChanged();
    }

    private void HandleProductsFetchFailed(ProductFetchFailed failure)
    {
        IsReady = false;
        StatusMessage = "PRODUKTE NICHT VERFÜGBAR // " + failure.FailureReason;
        RaiseChanged();
    }

    private async void HandlePurchasePending(PendingOrder order)
    {
        Product product = FirstProduct(order);
        string transactionId = order?.Info?.TransactionID;
        string validationKey = string.IsNullOrWhiteSpace(transactionId)
            ? product?.definition?.id + ":" + (order?.Info?.Receipt?.GetHashCode() ?? 0)
            : transactionId;
        if (product == null || string.IsNullOrWhiteSpace(validationKey) || !validatingTransactions.Add(validationKey)) return;

        StatusMessage = "KAUF WIRD SICHER GEPRÜFT...";
        RaiseChanged();
        try
        {
            NavalPurchaseRequest request = CreateValidationRequest(order, product);
            NavalPurchaseResult result = await onlineService.ValidatePurchaseAsync(request);
            if (result?.verified != true)
                throw new InvalidOperationException("KAUF NICHT BESTÄTIGT");

            // Confirmation deliberately happens only after the server grants the entitlement.
            store.ConfirmPurchase(order);
            StatusMessage = "KAUF BESTÄTIGT // " + product.definition.id.ToUpperInvariant();
        }
        catch (Exception exception)
        {
            // Leave the order pending. Unity IAP will deliver it again after a restart.
            StatusMessage = "PRÜFUNG FEHLGESCHLAGEN // " + SafeMessage(exception);
        }
        finally
        {
            validatingTransactions.Remove(validationKey);
            RaiseChanged();
        }
    }

    private void HandlePurchaseConfirmed(Order order)
    {
        Product product = FirstProduct(order);
        StatusMessage = "FREIGESCHALTET // " + (product?.definition?.id ?? "KAUF").ToUpperInvariant();
        RaiseChanged();
    }

    private void HandlePurchaseFailed(FailedOrder order)
    {
        StatusMessage = "KAUF ABGEBROCHEN // " + order.FailureReason;
        RaiseChanged();
    }

    private void HandlePurchaseDeferred(DeferredOrder order)
    {
        StatusMessage = "KAUF WARTET AUF FREIGABE";
        RaiseChanged();
    }

    private static NavalPurchaseRequest CreateValidationRequest(PendingOrder order, Product product)
    {
        string receipt = order.Info?.Receipt ?? string.Empty;
        string signature = string.Empty;
        NavalStorePlatform platform;

        if (order.Info?.Google != null)
        {
            platform = NavalStorePlatform.Google;
            GoogleReceiptPayload payload = ParseGoogleReceipt(receipt);
            receipt = payload.json;
            signature = payload.signature;
        }
        else if (order.Info?.Apple != null)
        {
            platform = NavalStorePlatform.Apple;
            if (!string.IsNullOrWhiteSpace(order.Info.Apple.AppReceipt))
                receipt = order.Info.Apple.AppReceipt;
        }
        else
        {
            throw new InvalidOperationException("NICHT UNTERSTÜTZTER STORE");
        }

        if (string.IsNullOrWhiteSpace(receipt) || (platform == NavalStorePlatform.Google && string.IsNullOrWhiteSpace(signature)))
            throw new InvalidOperationException("STORE-BELEG UNVOLLSTÄNDIG");

        ProductMetadata metadata = product.metadata;
        return new NavalPurchaseRequest
        {
            platform = platform,
            productId = product.definition.id,
            receipt = receipt,
            signature = signature,
            localCostMinorUnits = ToMinorUnits(metadata?.localizedPrice ?? 0m, metadata?.isoCurrencyCode),
            localCurrency = string.IsNullOrWhiteSpace(metadata?.isoCurrencyCode) ? "EUR" : metadata.isoCurrencyCode
        };
    }

    private static GoogleReceiptPayload ParseGoogleReceipt(string raw)
    {
        GoogleReceiptPayload payload = JsonUtility.FromJson<GoogleReceiptPayload>(raw);
        if (!string.IsNullOrWhiteSpace(payload?.json)) return payload;
        UnifiedReceipt unified = JsonUtility.FromJson<UnifiedReceipt>(raw);
        return string.IsNullOrWhiteSpace(unified?.Payload)
            ? new GoogleReceiptPayload()
            : JsonUtility.FromJson<GoogleReceiptPayload>(unified.Payload);
    }

    private static int ToMinorUnits(decimal price, string currency)
    {
        string code = (currency ?? string.Empty).ToUpperInvariant();
        int decimals = code == "JPY" || code == "KRW" || code == "VND" ? 0
            : code == "BHD" || code == "JOD" || code == "KWD" || code == "OMR" || code == "TND" ? 3
            : 2;
        decimal multiplier = decimals == 0 ? 1m : decimals == 3 ? 1000m : 100m;
        return decimal.ToInt32(decimal.Round(price * multiplier, 0, MidpointRounding.AwayFromZero));
    }

    private Product FindProduct(string productId)
        => store?.GetProducts()?.FirstOrDefault(product => product.definition.id == productId);

    internal static void ReplaceLegacyFakeStoreInputModule()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        StandaloneInputModule[] legacyModules = UnityEngine.Object.FindObjectsByType<StandaloneInputModule>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (StandaloneInputModule legacyModule in legacyModules)
        {
            if (legacyModule == null) continue;
            GameObject eventSystemObject = legacyModule.gameObject;
            legacyModule.enabled = false;
            if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null)
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(legacyModule);
            else
                UnityEngine.Object.DestroyImmediate(legacyModule);
        }
#endif
    }

    private static Product FirstProduct(Order order)
        => order?.CartOrdered?.Items()?.FirstOrDefault()?.Product;

    private static string SafeMessage(Exception exception)
        => string.IsNullOrWhiteSpace(exception?.Message) ? "UNBEKANNTER FEHLER" : exception.Message.ToUpperInvariant();

    private void RaiseChanged() => Changed?.Invoke();

    [Serializable]
    private sealed class GoogleReceiptPayload
    {
        public string json;
        public string signature;
    }

    [Serializable]
    private sealed class UnifiedReceipt
    {
        public string Payload;
    }
}
