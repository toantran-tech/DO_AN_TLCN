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
    }
}
