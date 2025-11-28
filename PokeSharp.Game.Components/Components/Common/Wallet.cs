namespace PokeSharp.Game.Components.Common;

/// <summary>
///     Component representing an entity's currency holdings.
///     Supports players, trainers, and NPCs that participate in economic actions.
/// </summary>
public struct Wallet
{
    /// <summary>
    ///     Identifier describing the currency type (e.g., "pokedollar").
    /// </summary>
    public string CurrencyId { get; set; }

    /// <summary>
    ///     Current balance held by the entity in the specified currency.
    /// </summary>
    public int Balance { get; set; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Wallet" /> struct.
    /// </summary>
    /// <param name="balance">Initial balance; negative values are clamped to zero.</param>
    /// <param name="currencyId">Currency identifier; defaults to <see cref="DefaultCurrencyId" />.</param>
    public Wallet(int balance = 0, string? currencyId = null)
    {
        Balance = balance < 0 ? 0 : balance;
        CurrencyId = string.IsNullOrWhiteSpace(currencyId) ? DefaultCurrencyId : currencyId;
    }

    /// <summary>
    ///     Default currency identifier used when none is provided.
    /// </summary>
    public const string DefaultCurrencyId = "pokedollar";
}
