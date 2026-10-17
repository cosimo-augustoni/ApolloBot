namespace Apollo.Domain.Messages;

public static class MessageContentPartsExtensions
{
    extension(MessageContentParts)
    {
        /// <summary>
        /// The parts of <paramref name="content"/>, with mentions parsed from the text (<c>&lt;@id&gt;</c>,
        /// <c>&lt;@&amp;id&gt;</c>, <c>@everyone</c>, <c>@here</c>). The content never leaves this call.
        /// </summary>
        /// <param name="content">The message text.</param>
        /// <param name="attachmentCount">How many attachments the message has.</param>
        public static MessageContentParts From(string content, int attachmentCount = 0) =>
            MessageMetadata.From(content, attachmentCount, embedCount: 0).ContentParts;
    }
}
