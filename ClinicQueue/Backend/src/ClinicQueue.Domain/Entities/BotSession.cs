namespace ClinicQueue.Domain.Entities;

public class BotSession
{
    private BotSession()
    {
    }

    public string Id { get; private set; } = string.Empty;
    public string ChannelUserId { get; private set; } = string.Empty;
    public string State { get; private set; } = "Idle";
    public string LanguageCode { get; private set; } = "eng_Latn";
    public string LastIntent { get; private set; } = "Other";
    public bool IsLocked { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }

    public static BotSession Start(string channelUserId)
    {
        if (string.IsNullOrWhiteSpace(channelUserId))
        {
            throw new ArgumentException("Channel user id is required.", nameof(channelUserId));
        }

        return new BotSession
        {
            Id = Guid.NewGuid().ToString(),
            ChannelUserId = channelUserId.Trim(),
            LastUpdatedAt = DateTime.UtcNow
        };
    }

    public void AdvanceState(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("State is required.", nameof(state));
        }

        State = state.Trim();
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void SetLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            throw new ArgumentException("Language code is required.", nameof(languageCode));
        }

        LanguageCode = languageCode.Trim();
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void SetIntent(string intent)
    {
        LastIntent = string.IsNullOrWhiteSpace(intent) ? "Other" : intent.Trim();
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void Lock()
    {
        IsLocked = true;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void Unlock()
    {
        IsLocked = false;
        LastUpdatedAt = DateTime.UtcNow;
    }
}
