namespace WashGo.Application.Contracts.Features.AuthContracts.Results
{
    public class RegisterResultDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    public class VerifyOtpResultDto
    {
        public bool Verified { get; set; }
        public string? VerificationToken { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class AuthResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public AuthUserDto User { get; set; } = null!;
    }

    public class AuthUserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        /// <summary>True only on the very first successful login of the account.</summary>
        public bool FirstLogin { get; set; }
        /// <summary>Customer has not accepted the current terms version yet (first login or a new version): show the terms.</summary>
        public bool RequiresTermsAcceptance { get; set; }
        public string? TermsVersion { get; set; }
    }
}
