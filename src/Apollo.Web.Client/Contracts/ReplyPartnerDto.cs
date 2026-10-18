namespace Apollo.Web.Client.Contracts;

/// <summary>A member on the other end of <paramref name="Count"/> replies.</summary>
public sealed record ReplyPartnerDto(EventUserDto User, int Count);
