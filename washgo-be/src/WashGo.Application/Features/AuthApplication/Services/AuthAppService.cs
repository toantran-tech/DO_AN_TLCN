using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.AuthContracts.Commands;
using WashGo.Application.Contracts.Features.AuthContracts.Results;
using WashGo.Application.Contracts.Features.AuthContracts.Services;
using WashGo.Core.Domain.Common;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.AuthApplication.Services
{
    public class AuthAppService : WashGoAppService, IAuthAppService
    {
        private const string VerifiedRegistrationPrefix = "verified-register:";
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IRepository<Role, Guid> _roleRepo;
        private readonly IRepository<RefreshToken, Guid> _refreshTokenRepo;
        private readonly IRepository<OtpCode, Guid> _otpRepo;
        private readonly IRepository<Customer, Guid> _customerRepo;
        private readonly IConfiguration _configuration;
        private readonly IAuthEmailSender _authEmailSender;

        public AuthAppService(
            IRepository<User, Guid> userRepo,
            IRepository<Role, Guid> roleRepo,
            IRepository<RefreshToken, Guid> refreshTokenRepo,
            IRepository<OtpCode, Guid> otpRepo,
            IRepository<Customer, Guid> customerRepo,
            IConfiguration configuration,
            IAuthEmailSender authEmailSender)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _refreshTokenRepo = refreshTokenRepo;
            _otpRepo = otpRepo;
            _customerRepo = customerRepo;
            _configuration = configuration;
            _authEmailSender = authEmailSender;
        }

        public async Task<BaseResponse<AuthResponseDto>> LoginAsync(LoginCommand command)
        {
            if (!IsValidEmail(command.Email) || string.IsNullOrWhiteSpace(command.Password))
            {
                return BadRequest<AuthResponseDto>("Email hoặc mật khẩu không chính xác.");
            }

            var email = command.Email.Trim().ToLowerInvariant();
            var user = await _userRepo.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (user == null)
            {
                return BadRequest<AuthResponseDto>("Email hoặc mật khẩu không chính xác.");
            }

            if (!user.IsActive)
            {
                return BadRequest<AuthResponseDto>("Tài khoản đã bị tạm khóa. Vui lòng liên hệ quản trị viên.");
            }

            bool isPasswordValid = false;
            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(command.Password, user.PasswordHash);
            }

            if (!isPasswordValid)
            {
                return BadRequest<AuthResponseDto>("Email hoặc mật khẩu không chính xác.");
            }

            var role = await _roleRepo.FindAsync(user.RoleId);
            var roleName = role?.Name ?? "Customer";

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user, autoSave: true);

            var (refreshToken, sessionId) = await GenerateAndSaveRefreshTokenAsync(user.Id);
            var (accessToken, expiresAt) = GenerateJwtToken(user, roleName, sessionId);

            var result = new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = new AuthUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Phone = user.Phone,
                    Role = roleName,
                    Avatar = user.Avatar
                }
            };

            return Success(result, "Đăng nhập thành công!");
        }

        public Task<BaseResponse<AuthResponseDto>> GoogleLoginAsync(GoogleLoginCommand command)
        {
            return GoogleLoginInternalAsync(command, allowCustomerRegistration: true);
        }

        public Task<BaseResponse<AuthResponseDto>> GoogleAdminLoginAsync(GoogleLoginCommand command)
        {
            return GoogleLoginInternalAsync(command, allowCustomerRegistration: false);
        }

        private async Task<BaseResponse<AuthResponseDto>> GoogleLoginInternalAsync(
            GoogleLoginCommand command,
            bool allowCustomerRegistration)
        {
            var (payload, errorMessage) = await ValidateGoogleIdTokenAsync(command.IdToken);
            if (payload == null)
            {
                return BadRequest<AuthResponseDto>(errorMessage!);
            }

            var email = payload.Email.Trim().ToLowerInvariant();
            var user = await _userRepo.FirstOrDefaultAsync(u =>
                u.AuthProvider == AuthProvider.Google && u.ProviderId == payload.Subject);

            if (user == null)
            {
                if (!allowCustomerRegistration)
                {
                    return BadRequest<AuthResponseDto>(
                        "Tài khoản quản trị này chưa được liên kết Google. Vui lòng đăng nhập bằng mật khẩu trước.");
                }

                var existingEmailUser = await _userRepo.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
                if (existingEmailUser != null)
                {
                    var existingRole = await _roleRepo.FindAsync(existingEmailUser.RoleId);
                    if (!string.Equals(existingRole?.Name, "Customer", StringComparison.OrdinalIgnoreCase))
                        return BadRequest<AuthResponseDto>("Tài khoản này không thuộc Customer. Vui lòng dùng cổng quản trị.");
                    if (!existingEmailUser.IsActive)
                        return BadRequest<AuthResponseDto>("Tài khoản đã bị tạm khóa. Vui lòng liên hệ quản trị viên.");
                    if (existingEmailUser.AuthProvider == AuthProvider.Google &&
                        existingEmailUser.ProviderId != payload.Subject)
                        return BadRequest<AuthResponseDto>("Email này đã liên kết với tài khoản Google khác.");

                    // Google has verified ownership of this email, so reuse the existing Customer.
                    existingEmailUser.AuthProvider = AuthProvider.Google;
                    existingEmailUser.ProviderId = payload.Subject;
                    existingEmailUser.Avatar ??= payload.Picture;
                    user = existingEmailUser;
                }
                else
                {
                    var customerRole = await _roleRepo.FirstOrDefaultAsync(r => r.Name == "Customer");
                    if (customerRole == null)
                        return BadRequest<AuthResponseDto>("Không tìm thấy vai trò Customer trong hệ thống.");

                    user = new User(Guid.NewGuid())
                    {
                        RoleId = customerRole.Id,
                        Email = email,
                        FullName = string.IsNullOrWhiteSpace(payload.Name) ? email : payload.Name.Trim(),
                        Avatar = payload.Picture,
                        AuthProvider = AuthProvider.Google,
                        ProviderId = payload.Subject,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _userRepo.InsertAsync(user, autoSave: true);

                    var customer = new Customer(user.Id)
                    {
                        LoyaltyPoints = 0,
                        TotalOrders = 0,
                        TotalSpent = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _customerRepo.InsertAsync(customer, autoSave: true);
                }
            }

            if (!user.IsActive)
            {
                return BadRequest<AuthResponseDto>("Tài khoản đã bị tạm khóa. Vui lòng liên hệ quản trị viên.");
            }

            var role = await _roleRepo.FindAsync(user.RoleId);
            if (role == null)
                return BadRequest<AuthResponseDto>("Tài khoản không có vai trò hợp lệ.");
            var roleName = role.Name;
            if (allowCustomerRegistration && !roleName.Equals("Customer", StringComparison.OrdinalIgnoreCase))
                return BadRequest<AuthResponseDto>("Tài khoản này không thuộc Customer. Vui lòng dùng cổng quản trị.");
            if (!allowCustomerRegistration && roleName.Equals("Customer", StringComparison.OrdinalIgnoreCase))
                return BadRequest<AuthResponseDto>("Tài khoản Customer không thể đăng nhập vào trang quản trị.");

            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user, autoSave: true);

            var (refreshToken, sessionId) = await GenerateAndSaveRefreshTokenAsync(user.Id);
            var (accessToken, expiresAt) = GenerateJwtToken(user, roleName, sessionId);

            return Success(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = new AuthUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Phone = user.Phone,
                    Role = roleName,
                    Avatar = user.Avatar
                }
            }, "Đăng nhập bằng Google thành công!");
        }

        public async Task<BaseResponse<bool>> LinkGoogleAsync(Guid userId, LinkGoogleCommand command)
        {
            var user = await _userRepo.FindAsync(userId);
            if (user == null || !user.IsActive)
            {
                return BadRequest<bool>("Tài khoản không tồn tại hoặc đã bị khóa.");
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash) ||
                string.IsNullOrWhiteSpace(command.CurrentPassword) ||
                !BCrypt.Net.BCrypt.Verify(command.CurrentPassword, user.PasswordHash))
            {
                return BadRequest<bool>("Mật khẩu xác nhận không chính xác.");
            }

            var (payload, errorMessage) = await ValidateGoogleIdTokenAsync(command.IdToken);
            if (payload == null)
            {
                return BadRequest<bool>(errorMessage!);
            }

            var linkedUser = await _userRepo.FirstOrDefaultAsync(u =>
                u.AuthProvider == AuthProvider.Google && u.ProviderId == payload.Subject);
            if (linkedUser != null && linkedUser.Id != user.Id)
            {
                return BadRequest<bool>("Tài khoản Google này đã được liên kết với một tài khoản WashGo khác.");
            }

            user.AuthProvider = AuthProvider.Google;
            user.ProviderId = payload.Subject;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepo.UpdateAsync(user, autoSave: true);

            return Success(true, "Đã liên kết Google. Lần sau bạn có thể đăng nhập bằng Google.");
        }

        public async Task<BaseResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.RefreshToken))
            {
                return BadRequest<AuthResponseDto>("Refresh token không hợp lệ hoặc đã hết hạn.");
            }

            var tokenHash = HashRefreshToken(command.RefreshToken);
            var storedToken = await _refreshTokenRepo.FirstOrDefaultAsync(t => t.Token == tokenHash);
            if (storedToken == null || storedToken.IsRevoked || storedToken.ExpiryDate <= DateTime.UtcNow)
            {
                return BadRequest<AuthResponseDto>("Refresh token không hợp lệ hoặc đã hết hạn.");
            }

            var user = await _userRepo.FindAsync(storedToken.UserId);
            if (user == null || !user.IsActive)
            {
                return BadRequest<AuthResponseDto>("Tài khoản không tồn tại hoặc đã bị khóa.");
            }

            // Revoke old token
            storedToken.IsRevoked = true;
            await _refreshTokenRepo.UpdateAsync(storedToken, autoSave: true);

            var role = await _roleRepo.FindAsync(user.RoleId);
            var roleName = role?.Name ?? "Customer";

            var (newRefreshToken, sessionId) = await GenerateAndSaveRefreshTokenAsync(user.Id);
            var (accessToken, expiresAt) = GenerateJwtToken(user, roleName, sessionId);

            var result = new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = expiresAt,
                User = new AuthUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Phone = user.Phone,
                    Role = roleName,
                    Avatar = user.Avatar
                }
            };

            return Success(result, "Làm mới phiên đăng nhập thành công!");
        }

        public async Task<BaseResponse<bool>> LogoutAsync(Guid userId, Guid sessionId, LogoutCommand command)
        {
            var currentSession = await _refreshTokenRepo.FindAsync(sessionId);
            if (currentSession != null && currentSession.UserId == userId && !currentSession.IsRevoked)
            {
                currentSession.IsRevoked = true;
                await _refreshTokenRepo.UpdateAsync(currentSession, autoSave: true);
            }

            if (!string.IsNullOrWhiteSpace(command.RefreshToken))
            {
                var tokenHash = HashRefreshToken(command.RefreshToken);
                var storedToken = await _refreshTokenRepo.FirstOrDefaultAsync(t =>
                    t.Token == tokenHash && t.UserId == userId);
                if (storedToken != null && !storedToken.IsRevoked)
                {
                    storedToken.IsRevoked = true;
                    await _refreshTokenRepo.UpdateAsync(storedToken, autoSave: true);
                }
            }

            return Success(true, "Đăng xuất thành công!");
        }

        public async Task<BaseResponse<RegisterResultDto>> RegisterAsync(RegisterCommand command)
        {
            if (!IsValidEmail(command.Email))
            {
                return BadRequest<RegisterResultDto>("Email không đúng định dạng.");
            }

            var passwordError = ValidatePassword(command.Password);
            if (passwordError != null)
            {
                return BadRequest<RegisterResultDto>(passwordError);
            }
            if (command.Password != command.ConfirmPassword)
            {
                return BadRequest<RegisterResultDto>("Xác nhận mật khẩu không khớp.");
            }

            if (string.IsNullOrWhiteSpace(command.FullName) || command.FullName.Trim().Length is < 2 or > 100)
            {
                return BadRequest<RegisterResultDto>("Họ tên phải có từ 2 đến 100 ký tự.");
            }

            if (string.IsNullOrWhiteSpace(command.Phone) || !IsValidPhone(command.Phone))
            {
                return BadRequest<RegisterResultDto>("Vui lòng nhập số điện thoại hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(command.VerificationToken))
            {
                return BadRequest<RegisterResultDto>("Vui lòng xác minh email trước khi đăng ký.");
            }

            var email = command.Email.Trim().ToLowerInvariant();
            var existingUser = await _userRepo.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (existingUser != null)
            {
                return BadRequest<RegisterResultDto>($"Email '{command.Email}' đã được đăng ký.");
            }

            var activeOtps = await _otpRepo.GetListAsync(o =>
                o.Email.ToLower() == email &&
                o.Purpose == OtpPurpose.Register &&
                !o.IsUsed &&
                o.ExpiryDate > DateTime.UtcNow);
            var validOtp = activeOtps.OrderByDescending(o => o.CreatedAt).FirstOrDefault();

            if (validOtp == null || !validOtp.CodeHash.StartsWith(VerifiedRegistrationPrefix, StringComparison.Ordinal))
            {
                return BadRequest<RegisterResultDto>("Email chưa được xác minh hoặc phiên xác minh đã hết hạn.");
            }

            if (!BCrypt.Net.BCrypt.Verify(command.VerificationToken, validOtp.CodeHash[VerifiedRegistrationPrefix.Length..]))
            {
                return BadRequest<RegisterResultDto>("Phiên xác minh email không hợp lệ.");
            }

            // Public registration can only create Customer accounts. Merchant, Shipper
            // and Admin accounts must be provisioned by an authorized back-office flow.
            var role = await _roleRepo.FirstOrDefaultAsync(r => r.Name == "Customer");

            if (role == null)
            {
                return BadRequest<RegisterResultDto>("Không tìm thấy vai trò phù hợp trong hệ thống.");
            }

            validOtp.IsUsed = true;
            validOtp.UsedAt = DateTime.UtcNow;
            await _otpRepo.UpdateAsync(validOtp, autoSave: true);

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);
            var userId = Guid.NewGuid();

            var user = new User(userId)
            {
                RoleId = role.Id,
                Email = email,
                PasswordHash = passwordHash,
                FullName = command.FullName.Trim(),
                Phone = NormalizePhone(command.Phone),
                AuthProvider = AuthProvider.Local,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _userRepo.InsertAsync(user, autoSave: true);

            // If Customer role, create Customer profile
            if (role.Name.Equals("Customer", StringComparison.OrdinalIgnoreCase))
            {
                var customer = new Customer(userId)
                {
                    LoyaltyPoints = 0,
                    TotalOrders = 0,
                    TotalSpent = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _customerRepo.InsertAsync(customer, autoSave: true);
            }

            return Created(new RegisterResultDto { Id = user.Id, Email = user.Email },
                "Đăng ký tài khoản thành công! Vui lòng đăng nhập.");
        }

        public async Task<BaseResponse<bool>> SendOtpAsync(SendOtpCommand command)
        {
            if (!IsValidEmail(command.Email))
            {
                return BadRequest<bool>("Email không đúng định dạng.");
            }

            if (command.Purpose != OtpPurpose.ForgotPassword && command.Purpose != OtpPurpose.Register)
            {
                return BadRequest<bool>("Loại OTP này hiện chưa được hỗ trợ.");
            }

            if (command.Purpose == OtpPurpose.Register)
            {
                if (string.IsNullOrWhiteSpace(command.FullName) || command.FullName.Trim().Length is < 2 or > 100)
                    return BadRequest<bool>("Họ tên phải có từ 2 đến 100 ký tự.");
                if (string.IsNullOrWhiteSpace(command.Phone) || !IsValidPhone(command.Phone))
                    return BadRequest<bool>("Vui lòng nhập số điện thoại hợp lệ.");
                var passwordError = ValidatePassword(command.Password);
                if (passwordError != null)
                    return BadRequest<bool>(passwordError);
                if (command.Password != command.ConfirmPassword)
                    return BadRequest<bool>("Xác nhận mật khẩu không khớp.");
            }

            var email = command.Email.Trim().ToLowerInvariant();
            var user = await _userRepo.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (command.Purpose == OtpPurpose.Register)
            {
                if (user != null)
                {
                    return BadRequest<bool>($"Email '{command.Email}' đã được đăng ký trong hệ thống.");
                }
            }
            else if (command.Purpose == OtpPurpose.ForgotPassword)
            {
                // Do not disclose whether an account exists.
                if (user == null)
                {
                    return Success(true, "Nếu email tồn tại, mã OTP đã được gửi. Vui lòng kiểm tra hộp thư.");
                }
            }

            var activeOtps = await _otpRepo.GetListAsync(o =>
                o.Email.ToLower() == email &&
                o.Purpose == command.Purpose &&
                !o.IsUsed &&
                o.ExpiryDate > DateTime.UtcNow);

            if (activeOtps.Any(o => o.CreatedAt > DateTime.UtcNow.AddSeconds(-60)))
            {
                return BadRequest<bool>("Vui lòng chờ 60 giây trước khi yêu cầu mã OTP mới.");
            }

            foreach (var activeOtp in activeOtps)
            {
                activeOtp.IsUsed = true;
                activeOtp.UsedAt = DateTime.UtcNow;
                await _otpRepo.UpdateAsync(activeOtp, autoSave: true);
            }

            var randomCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            var codeHash = BCrypt.Net.BCrypt.HashPassword(randomCode);

            if (!await _authEmailSender.SendOtpAsync(email, randomCode, command.Purpose))
            {
                return BadRequest<bool>("Không thể gửi mã OTP lúc này. Vui lòng thử lại sau.");
            }

            var otp = new OtpCode
            {
                UserId = user?.Id,
                Email = email,
                CodeHash = codeHash,
                Purpose = command.Purpose,
                ExpiryDate = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
                AttemptCount = 0,
                MaxAttempts = 5,
                CreatedAt = DateTime.UtcNow
            };

            await _otpRepo.InsertAsync(otp, autoSave: true);

            var successMsg = command.Purpose == OtpPurpose.Register
                ? "Mã xác thực OTP đã được gửi về email của bạn. Vui lòng kiểm tra hộp thư."
                : "Nếu email tồn tại, mã OTP đã được gửi. Vui lòng kiểm tra hộp thư.";

            return Success(true, successMsg);
        }

        public async Task<BaseResponse<VerifyOtpResultDto>> VerifyOtpAsync(VerifyOtpCommand command)
        {
            if (!IsValidEmail(command.Email) || string.IsNullOrWhiteSpace(command.Code) || command.Code.Length != 6)
            {
                return BadRequest<VerifyOtpResultDto>("Mã OTP không hợp lệ hoặc đã hết hạn.");
            }

            if (command.Purpose != OtpPurpose.ForgotPassword && command.Purpose != OtpPurpose.Register)
            {
                return BadRequest<VerifyOtpResultDto>("Loại OTP này hiện chưa được hỗ trợ.");
            }

            var email = command.Email.Trim().ToLowerInvariant();
            var activeOtps = await _otpRepo.GetListAsync(o =>
                o.Email.ToLower() == email &&
                o.Purpose == command.Purpose &&
                !o.IsUsed &&
                o.ExpiryDate > DateTime.UtcNow);
            var validOtp = activeOtps.OrderByDescending(o => o.CreatedAt).FirstOrDefault();

            if (validOtp == null)
            {
                return BadRequest<VerifyOtpResultDto>("Mã OTP không hợp lệ hoặc đã hết hạn.");
            }

            if (validOtp.CodeHash.StartsWith(VerifiedRegistrationPrefix, StringComparison.Ordinal))
            {
                return BadRequest<VerifyOtpResultDto>("Mã OTP đã được xác minh.");
            }

            if (validOtp.AttemptCount >= validOtp.MaxAttempts)
            {
                return BadRequest<VerifyOtpResultDto>("Bạn đã nhập sai mã OTP quá số lần quy định.");
            }

            bool isValid = BCrypt.Net.BCrypt.Verify(command.Code, validOtp.CodeHash);
            if (!isValid)
            {
                validOtp.AttemptCount++;
                await _otpRepo.UpdateAsync(validOtp, autoSave: true);
                return BadRequest<VerifyOtpResultDto>($"Mã OTP không chính xác. Còn lại {validOtp.MaxAttempts - validOtp.AttemptCount} lần thử.");
            }

            if (command.Purpose == OtpPurpose.Register)
            {
                var verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                validOtp.CodeHash = VerifiedRegistrationPrefix + BCrypt.Net.BCrypt.HashPassword(verificationToken);
                validOtp.ExpiryDate = DateTime.UtcNow.AddMinutes(10);
                await _otpRepo.UpdateAsync(validOtp, autoSave: true);
                return Success(new VerifyOtpResultDto { Verified = true, VerificationToken = verificationToken },
                    "Xác minh email thành công!");
            }

            validOtp.IsUsed = true;
            validOtp.UsedAt = DateTime.UtcNow;
            await _otpRepo.UpdateAsync(validOtp, autoSave: true);

            return Success(new VerifyOtpResultDto { Verified = true }, "Xác thực mã OTP thành công!");
        }

        public async Task<BaseResponse<bool>> ResetPasswordAsync(ResetPasswordCommand command)
        {
            var passwordError = ValidatePassword(command.NewPassword);
            if (passwordError != null)
            {
                return BadRequest<bool>(passwordError);
            }
            if (command.NewPassword != command.ConfirmNewPassword)
            {
                return BadRequest<bool>("Xác nhận mật khẩu mới không khớp.");
            }

            var verifyResult = await VerifyOtpAsync(new VerifyOtpCommand
            {
                Email = command.Email,
                Code = command.Code,
                Purpose = OtpPurpose.ForgotPassword
            });

            if (!verifyResult.Succeeded)
            {
                return BadRequest<bool>(verifyResult.Message ?? "Mã xác thực OTP không hợp lệ.");
            }

            var email = command.Email.Trim().ToLowerInvariant();
            var user = await _userRepo.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
            if (user == null)
            {
                return NotFound<bool>("Không tìm thấy thông tin tài khoản người dùng.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(command.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepo.UpdateAsync(user, autoSave: true);
            await RevokeAllRefreshTokensAsync(user.Id);
            return Success(true, "Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.");
        }

        public async Task<BaseResponse<AuthUserDto>> GetCurrentUserAsync(Guid userId)
        {
            var user = await _userRepo.FindAsync(userId);
            if (user == null)
            {
                return NotFound<AuthUserDto>("Không tìm thấy thông tin người dùng.");
            }

            var role = await _roleRepo.FindAsync(user.RoleId);
            var result = new AuthUserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                Role = role?.Name ?? "Customer",
                Avatar = user.Avatar
            };

            return Success(result);
        }

        private (string token, DateTime expiresAt) GenerateJwtToken(User user, string roleName, Guid sessionId)
        {
            var secretKey = _configuration["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException("JWT secret key is not configured.");
            }
            var issuer = _configuration["Jwt:Issuer"] ?? "WashGoBackend";
            var audience = _configuration["Jwt:Audience"] ?? "WashGoClients";
            var expMinutes = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var m) ? m : 120;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresAt = DateTime.UtcNow.AddMinutes(expMinutes);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, roleName),
                new Claim("role", roleName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("session_id", sessionId.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }

        private async Task<(string token, Guid sessionId)> GenerateAndSaveRefreshTokenAsync(Guid userId)
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var refreshTokenString = Convert.ToBase64String(randomBytes);

            var expDays = int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var d) ? d : 30;

            var entity = new RefreshToken(Guid.NewGuid())
            {
                UserId = userId,
                Token = HashRefreshToken(refreshTokenString),
                ExpiryDate = DateTime.UtcNow.AddDays(expDays),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow
            };

            await _refreshTokenRepo.InsertAsync(entity, autoSave: true);
            return (refreshTokenString, entity.Id);
        }

        private async Task RevokeAllRefreshTokensAsync(Guid userId)
        {
            var activeTokens = await _refreshTokenRepo.GetListAsync(t => t.UserId == userId && !t.IsRevoked);
            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                await _refreshTokenRepo.UpdateAsync(token, autoSave: true);
            }
        }

        private async Task<(GoogleJsonWebSignature.Payload? Payload, string? ErrorMessage)> ValidateGoogleIdTokenAsync(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
            {
                return (null, "Google ID token không hợp lệ.");
            }

            var googleClientId = _configuration["Google:ClientId"];
            if (string.IsNullOrWhiteSpace(googleClientId))
            {
                return (null, "Google Sign-In chưa được cấu hình trên máy chủ.");
            }

            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(
                    idToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { googleClientId }
                    });

                if (string.IsNullOrWhiteSpace(payload.Subject) ||
                    !payload.EmailVerified ||
                    !IsValidEmail(payload.Email))
                {
                    return (null, "Tài khoản Google chưa xác thực email hợp lệ.");
                }

                return (payload, null);
            }
            catch
            {
                return (null, "Google ID token không hợp lệ hoặc đã hết hạn.");
            }
        }

        private static string HashRefreshToken(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }

        private static string? ValidatePassword(string? password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 128)
            {
                return "Mật khẩu phải có từ 8 đến 128 ký tự.";
            }

            if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
            {
                return "Mật khẩu phải có chữ hoa, chữ thường và chữ số.";
            }

            return null;
        }

        private static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Length > 100)
            {
                return false;
            }

            try
            {
                var address = new MailAddress(email.Trim());
                return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool IsValidPhone(string phone)
        {
            var normalized = new string(phone.Where(char.IsDigit).ToArray());
            return normalized.Length is >= 9 and <= 11;
        }

        private static string? NormalizePhone(string? phone)
        {
            return string.IsNullOrWhiteSpace(phone)
                ? null
                : new string(phone.Where(char.IsDigit).ToArray());
        }
    }
}
