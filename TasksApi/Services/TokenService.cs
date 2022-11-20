using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using TasksApi.Helpers;
using TasksApi.Interfaces;
using TasksApi.Models;
using TasksApi.Requests;
using TasksApi.Responses;

namespace TasksApi.Services
{
    public class TokenService : ITokenService
    {
        private readonly TasksDbContext _tasksDbContext;

        public TokenService(TasksDbContext tasksDbContext)
        {
            _tasksDbContext = tasksDbContext;
        }

        public async Task<Tuple<string, string>> GenerateTokensAsync(int userId)
        {
            var accessToken = await TokenHelper.GenerateAccessToken(userId);
            var refreshToken = await TokenHelper.GenerateRefreshToken();
            
            var userRecord = await _tasksDbContext.Users.Include(o => o.RefreshTokens).FirstOrDefaultAsync(e => e.Id == userId);
            if (userRecord == null)
            {
                return null;
            }
            
            var salt = PasswordHelper.GetSecureSalt();
            var refreshTokenHashed = PasswordHelper.HashUsingPbkdf2(refreshToken, salt);

            if (userRecord.RefreshTokens != null && userRecord.RefreshTokens.Any())
            {
                await RemoveRefreshTokenAsync(userRecord);
            }

            userRecord.RefreshTokens?.Add(new RefreshToken
            {
                ExpiryDate = DateAndTime.Now.AddDays(30),
                Ts = DateTime.Now,
                UserId = userRecord.Id,
                TokenHash = refreshTokenHashed,
                TokenSalt = Convert.ToBase64String(salt)
            });

            await _tasksDbContext.SaveChangesAsync();

            var token = new Tuple<string, string>(accessToken, refreshToken);

            return token;
        }

        public async Task<bool> RemoveRefreshTokenAsync(User user)
        {
            var userRecord = await _tasksDbContext.Users.Include(o => o.RefreshTokens).FirstOrDefaultAsync(e => e.Id == user.Id);

            if (userRecord == null)
            {
                return false;
            }

            if (userRecord.RefreshTokens != null && userRecord.RefreshTokens.Any())
            {
                var currentRefreshToken = userRecord.RefreshTokens.First();

                _tasksDbContext.RefreshTokens.Remove(currentRefreshToken);
            }

            return false;
        }

        public async Task<ValidateRefreshTokenResponse> ValidateRefreshTokenAsync(RefreshTokenRequest refreshTokenRequest)
        {
            var refreshToken = await _tasksDbContext.RefreshTokens.FirstOrDefaultAsync(o => o.UserId == refreshTokenRequest.UserId);

            var response = new ValidateRefreshTokenResponse();

            if (refreshToken == null)
            { 
                response.Success = false;
                response.Error = "Invalid session or user is already logged out";
                response.ErrorCode = "R02";

                return response;
            }

            var refreshTokenToValidateHash = PasswordHelper.HashUsingPbkdf2(refreshTokenRequest.RefreshToken, Convert.FromBase64String(refreshToken.TokenSalt));
            if (refreshToken.TokenHash != refreshTokenToValidateHash)
            {
                response.Success = false;
                response.Error = "Invalid refresh token";
                response.ErrorCode = "R03";

                return response;
            }

            if (refreshToken.ExpiryDate < DateTime.Now)
            {
                response.Success = false;
                response.Error = "Refresh token has expired";
                response.ErrorCode = "R04";

                return response;
            }

            response.Success = true;
            response.UserId = refreshToken.UserId;

            return response;
        }
    }
}
