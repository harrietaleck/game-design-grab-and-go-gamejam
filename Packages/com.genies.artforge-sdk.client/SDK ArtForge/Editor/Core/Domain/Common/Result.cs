namespace Genies.Sdk.ArtForge.Editor.Core.Domain.Common
{
    /// <summary>
    /// Represents the result of an operation that can succeed or fail.
    /// Implements the Result pattern to avoid exceptions for expected failures.
    /// </summary>
    internal class Result
    {
        public bool IsSuccess { get; }
        public string Error { get; }

        protected Result(bool isSuccess, string error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success() => new Result(true, null);
        public static Result Failure(string error) => new Result(false, error);
    }

    /// <summary>
    /// Generic result that carries a value on success.
    /// </summary>
    /// <typeparam name="T">The type of value returned on success</typeparam>
    internal class Result<T> : Result
    {
        public T Value { get; }

        private Result(bool isSuccess, T value, string error)
            : base(isSuccess, error)
        {
            Value = value;
        }

        public static Result<T> Success(T value) =>
            new Result<T>(true, value, null);

        public new static Result<T> Failure(string error) =>
            new Result<T>(false, default, error);
    }
}

