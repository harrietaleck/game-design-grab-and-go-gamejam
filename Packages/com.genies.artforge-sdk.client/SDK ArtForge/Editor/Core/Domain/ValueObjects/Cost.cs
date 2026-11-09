using System;
using System.Collections.Generic;
using Genies.Sdk.ArtForge.Editor.Core.Domain.Common;

namespace Genies.Sdk.ArtForge.Editor.Core.Domain.ValueObjects
{
    /// <summary>
    /// Value object representing a monetary cost.
    /// Provides safe arithmetic operations and validation.
    /// </summary>
    internal class Cost : ValueObject
    {
        private const int CurrencyCodeLength = 3;

        public decimal Amount { get; }
        public string Currency { get; }

        private Cost(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        /// <summary>
        /// Creates a new Cost value.
        /// </summary>
        /// <param name="amount">The monetary amount</param>
        /// <param name="currency">Currency code (e.g., "USD")</param>
        /// <returns>Result containing the Cost or an error message</returns>
        public static Result<Cost> Create(decimal amount, string currency = "USD")
        {
            if (amount < 0)
            {
                return Result<Cost>.Failure("Cost amount cannot be negative");
            }

            if (string.IsNullOrWhiteSpace(currency))
            {
                return Result<Cost>.Failure("Currency must be specified");
            }

            if (currency.Length != CurrencyCodeLength)
            {
                return Result<Cost>.Failure("Currency must be a 3-letter code (e.g., USD, EUR)");
            }

            return Result<Cost>.Success(new Cost(amount, currency.ToUpperInvariant()));
        }

        /// <summary>
        /// Creates a zero cost.
        /// </summary>
        public static Cost Zero(string currency = "USD") => new Cost(0, currency);

        /// <summary>
        /// Adds two costs together. They must have the same currency.
        /// </summary>
        public Result<Cost> Add(Cost other)
        {
            if (Currency != other.Currency)
            {
                return Result<Cost>.Failure($"Cannot add costs with different currencies: {Currency} and {other.Currency}");
            }

            return Result<Cost>.Success(new Cost(Amount + other.Amount, Currency));
        }

        /// <summary>
        /// Subtracts one cost from another. They must have the same currency.
        /// </summary>
        public Result<Cost> Subtract(Cost other)
        {
            if (Currency != other.Currency)
            {
                return Result<Cost>.Failure($"Cannot subtract costs with different currencies: {Currency} and {other.Currency}");
            }

            var newAmount = Amount - other.Amount;
            if (newAmount < 0)
            {
                return Result<Cost>.Failure("Subtraction would result in negative cost");
            }

            return Result<Cost>.Success(new Cost(newAmount, Currency));
        }

        /// <summary>
        /// Multiplies the cost by a factor.
        /// </summary>
        public Cost Multiply(decimal factor)
        {
            if (factor < 0)
            {
                throw new ArgumentException("Factor cannot be negative", nameof(factor));
            }

            return new Cost(Amount * factor, Currency);
        }

        /// <summary>
        /// Compares if this cost is greater than another.
        /// </summary>
        public bool GreaterThan(Cost other)
        {
            if (Currency != other.Currency)
            {
                throw new InvalidOperationException($"Cannot compare costs with different currencies: {Currency} and {other.Currency}");
            }

            return Amount > other.Amount;
        }

        /// <summary>
        /// Compares if this cost is less than another.
        /// </summary>
        public bool LessThan(Cost other)
        {
            if (Currency != other.Currency)
            {
                throw new InvalidOperationException($"Cannot compare costs with different currencies: {Currency} and {other.Currency}");
            }

            return Amount < other.Amount;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }

        public override string ToString()
        {
            return $"{Amount:F2} {Currency}";
        }
    }
}
