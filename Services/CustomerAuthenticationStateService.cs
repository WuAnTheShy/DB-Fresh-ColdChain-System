using System.Collections.Concurrent;
using System.Security.Cryptography;
using FreshColdChain.Models;

namespace FreshColdChain.Services;

// 保存模拟短信验证码和消费者登录版本。当前项目的 Session 也使用进程内存，
// 因此进程重启时二者会同步失效，不需要新增数据库表。
public sealed class CustomerAuthenticationStateService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SendCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan FailureLockout = TimeSpan.FromMinutes(10);
    private const int MaxFailedAttempts = 5;

    private readonly ConcurrentDictionary<string, PasswordResetState> _passwordResetStates = new();
    private readonly ConcurrentDictionary<string, int> _authenticationVersions = new();

    public GroupBCustomerPasswordResetCodeResult IssuePasswordResetCode(string phone)
    {
        var now = DateTimeOffset.UtcNow;
        var state = _passwordResetStates.GetOrAdd(phone, _ => new PasswordResetState());

        lock (state.Gate)
        {
            if (state.LockedUntil is { } lockedUntil && lockedUntil > now)
                throw new GroupBBusinessException("验证码尝试次数过多，请10分钟后重试");

            var nextSendAt = state.LastSentAt + SendCooldown;
            if (state.LastSentAt != default && nextSendAt > now)
            {
                var seconds = Math.Max(1, (int)Math.Ceiling((nextSendAt - now).TotalSeconds));
                throw new GroupBBusinessException($"验证码发送过于频繁，请{seconds}秒后重试");
            }

            state.VerificationId = Guid.NewGuid().ToString("N");
            state.Code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            state.ExpiresAt = now + CodeLifetime;
            state.LastSentAt = now;
            state.FailedAttempts = 0;
            state.LockedUntil = null;

            return new GroupBCustomerPasswordResetCodeResult
            {
                VerificationId = state.VerificationId,
                SimulatedCode = state.Code,
                ExpiresAt = state.ExpiresAt,
                ExpiresInSeconds = (int)CodeLifetime.TotalSeconds
            };
        }
    }

    public void ConsumePasswordResetCode(
        string phone,
        string verificationId,
        string code)
    {
        if (!_passwordResetStates.TryGetValue(phone, out var state))
            throw new GroupBBusinessException("验证码已失效，请重新获取");

        var now = DateTimeOffset.UtcNow;
        lock (state.Gate)
        {
            if (state.LockedUntil is { } lockedUntil && lockedUntil > now)
                throw new GroupBBusinessException("验证码尝试次数过多，请10分钟后重试");

            if (state.ExpiresAt <= now ||
                !string.Equals(state.VerificationId, verificationId, StringComparison.Ordinal))
            {
                _passwordResetStates.TryRemove(phone, out _);
                throw new GroupBBusinessException("验证码已失效，请重新获取");
            }

            if (!string.Equals(state.Code, code, StringComparison.Ordinal))
            {
                state.FailedAttempts++;
                if (state.FailedAttempts >= MaxFailedAttempts)
                {
                    state.LockedUntil = now + FailureLockout;
                    throw new GroupBBusinessException("验证码尝试次数过多，请10分钟后重试");
                }

                throw new GroupBBusinessException(
                    $"验证码错误，还可尝试{MaxFailedAttempts - state.FailedAttempts}次");
            }

            _passwordResetStates.TryRemove(phone, out _);
        }
    }

    public int GetAuthenticationVersion(string customerId) =>
        _authenticationVersions.GetOrAdd(customerId, 0);

    public void InvalidateSessions(string customerId) =>
        _authenticationVersions.AddOrUpdate(customerId, 1, (_, version) => checked(version + 1));

    private sealed class PasswordResetState
    {
        public object Gate { get; } = new();
        public string VerificationId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset LastSentAt { get; set; }
        public int FailedAttempts { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }
}
