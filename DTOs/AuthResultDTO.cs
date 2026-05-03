namespace DTOs
{
    public record AuthResultDTO(
        UserProfileDTO User,
        string Token
    );
}
