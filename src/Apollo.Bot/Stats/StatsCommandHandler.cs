using Apollo.Bot.Links;
using Apollo.Bot.Resources;
using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Apollo.Bot.Stats;

/// <summary>
/// <c>/apollo stats</c> (scope D5): a link to the member's personal page <c>/me</c>, since stats live on the website
/// only (scope §3, §7.2). The same reply for everyone: the website decides who sees what, so it does not ask the
/// tracking gate.
/// </summary>
internal sealed class StatsCommandHandler(IOptions<LinksOptions> links, IStringLocalizer<Strings> strings) : ICommandHandler
{
    public string Path => "apollo stats";

    public Task<CommandReply> HandleAsync(CommandInvocation invocation, CancellationToken cancellationToken) =>
        Task.FromResult(CommandReply.Private(strings["Stats_Link", links.Value.GetMePageUrl()]));
}
