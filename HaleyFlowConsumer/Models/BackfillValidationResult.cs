using System;
using System.Collections.Generic;

namespace Haley.Models {
    public sealed class BackfillValidationResult {
        public bool                  IsValid   { get; init; }
        public IReadOnlyList<string> Errors    { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Warnings  { get; init; } = Array.Empty<string>();

        public static BackfillValidationResult Success(IReadOnlyList<string> warnings)
            => new() { IsValid = true, Warnings = warnings };

        public static BackfillValidationResult Fail(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
            => new() { IsValid = false, Errors = errors, Warnings = warnings };
    }
}
