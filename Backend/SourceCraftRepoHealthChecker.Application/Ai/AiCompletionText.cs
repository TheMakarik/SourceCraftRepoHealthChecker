using System.Text;
using Microsoft.Extensions.AI;

namespace SourceCraftRepoHealthChecker.Application.Ai;

public static class AiCompletionText
{
    public static (string Text, string Reasoning) Split(IEnumerable<AIContent> contents)
    {
        var text = new StringBuilder();
        var reasoning = new StringBuilder();
        foreach (var content in contents)
        {
            switch (content)
            {
                case TextContent textContent when !string.IsNullOrEmpty(textContent.Text):
                    text.Append(textContent.Text);
                    break;
                case TextReasoningContent reasoningContent when !string.IsNullOrEmpty(reasoningContent.Text):
                    reasoning.Append(reasoningContent.Text);
                    break;
            }
        }

        return (text.ToString(), reasoning.ToString());
    }

    public static (string Text, string Reasoning) Split(ChatResponse response)
    {
        var text = new StringBuilder();
        var reasoning = new StringBuilder();
        foreach (var message in response.Messages)
        {
            var (messageText, messageReasoning) = Split(message.Contents);
            text.Append(messageText);
            reasoning.Append(messageReasoning);
        }

        return (text.ToString(), reasoning.ToString());
    }

    public static string Best(ChatResponse response)
    {
        var (text, reasoning) = Split(response);
        return string.IsNullOrWhiteSpace(text) ? reasoning : text;
    }
}
