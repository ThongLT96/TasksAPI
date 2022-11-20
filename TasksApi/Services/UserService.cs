using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TasksApi.Helpers;
using TasksApi.Interfaces;
using TasksApi.Models;
using TasksApi.Requests;
using TasksApi.Responses;

namespace TasksApi.Services
{
    public class UserService : IUserService
    {
        private readonly TasksDbContext _tasksDbContext;
        private readonly ITokenService _tokenService;

        public UserService(TasksDbContext tasksDbContext, ITokenService tokenService)
        {
            _tasksDbContext = tasksDbContext;
            _tokenService = tokenService;
        }

        public async Task<TokenResponse> LoginAsync(LoginRequest loginRequest)
        {
            var user = _tasksDbContext.Users.SingleOrDefault(user => user.Active && user.Email == loginRequest.Email);

            if (user == null)
            {
                return new TokenResponse
                {
                    Success = false,
                    Error = "Email not found",
                    ErrorCode = "L02"
                };
            }

            var passwordHash = PasswordHelper.HashUsingPbkdf2(loginRequest.Password, Convert.FromBase64String(user.PasswordSalt));

            if (user.Password != passwordHash)
            {
                return new TokenResponse
                {
                    Success = false,
                    Error = "Invalid Password",
                    ErrorCode = "L03"
                };
            }

            var token = await System.Threading.Tasks.Task.Run(() => _tokenService.GenerateTokensAsync(user.Id));

            return new TokenResponse
            {
                Success = true,
                AccessToken = token.Item1,
                RefreshToken = token.Item2
            };
        }

        public async Task<LogoutResponse> LogoutAsync(int userId)
        {
            var refreshToken = await _tasksDbContext.RefreshTokens.FirstOrDefaultAsync(o => o.UserId == userId);

            if (refreshToken == null)
            {
                return new LogoutResponse {
                    Success = true
                };
            }

            _tasksDbContext.RefreshTokens.Remove(refreshToken);

            var saveResponse = await _tasksDbContext.SaveChangesAsync();

            if (saveResponse >= 0)
            {
                return new LogoutResponse {
                    Success = true
                };
            }

            return new LogoutResponse {
                Success = false,
                Error = "Unable to logout user",
                ErrorCode = "L04"
            };
        }

        public async Task<SignupResponse> SignupAsync(SignupRequest signupRequest)
        {
            var existingUser = await _tasksDbContext.Users.SingleOrDefaultAsync(user => user.Email == signupRequest.Email);

            if (existingUser != null)
            {
                return new SignupResponse { 
                    Success = false,
                    Error = "User already exist with the same email",
                    ErrorCode = "S02"
                };
            }

            if (signupRequest.Password != signupRequest.ConfirmPassword)
            {
                return new SignupResponse
                {
                    Success = false,
                    Error = "Password and confirm password do not match",
                    ErrorCode = "S03"
                };
            }

            if (signupRequest.Password.Length < 6)
            {
                return new SignupResponse
                {
                    Success = false,
                    Error = "Password is weak",
                    ErrorCode = "S04"
                };
            }

            var salt = PasswordHelper.GetSecureSalt();
            var passwordHash = PasswordHelper.HashUsingPbkdf2(signupRequest.Password, salt);

            var user = new User
            { 
                Email = signupRequest.Email,
                Password = passwordHash,
                PasswordSalt = Convert.ToBase64String(salt),
                FirstName = signupRequest.FirstName,
                LastName = signupRequest.LastName,
                Ts = signupRequest.Ts,
                Active = true
            };

            await _tasksDbContext.Users.AddAsync(user);

            var saveResponse = await _tasksDbContext.SaveChangesAsync();

            if (saveResponse >= 0)
            { 
                return new SignupResponse {
                    Success = true,
                    Email = user.Email
                };
            }

            return new SignupResponse
            { 
                Success = false,
                Error = "Unable to save the user",
                ErrorCode = "S05"
            };
        }
    }
}
