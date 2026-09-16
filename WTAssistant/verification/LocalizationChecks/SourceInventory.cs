using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

internal static class SourceInventory
{
    private sealed record Candidate(string File, int Line, string Surface, string Expression);

    public static void Write(string workspace, string output)
    {
        var candidates = new List<Candidate>();
        foreach (string folder in new[] { "WTAssistant", "Modules/VTrim/Integration/VTrim" })
        {
            foreach (string file in Directory.EnumerateFiles(Path.Combine(workspace, folder), "*.cs", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileName(file) == "Localization.cs") continue;
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    if (node is AssignmentExpressionSyntax assignment &&
                        assignment.Left.ToString().Split('.').Last() is "Text" or "HeaderText" or "Title" or "Description" or "Filter" or "AccessibleName")
                    {
                        if (HasLiteralText(assignment.Right)) Add(node, "property", assignment.ToString());
                    }
                    else if (node is InvocationExpressionSyntax call)
                    {
                        string name = call.Expression.ToString().Split('.').Last();
                        if (name is "SetToolTip" or "SetInstruction" or "SetStatus" or "CreatePrimaryButton" or
                            "CreateSecondaryButton" or "LabelAt" or "SetStatusText" || call.Expression.ToString() == "MessageBox.Show")
                        {
                            if (call.ArgumentList.Arguments.Any(arg => HasLiteralText(arg.Expression)))
                                Add(node, "call", call.ToString());
                        }
                    }
                }
                void Add(SyntaxNode node, string surface, string expression) => candidates.Add(new(
                    Path.GetRelativePath(workspace, file), tree.GetLineSpan(node.Span).StartLinePosition.Line + 1,
                    surface, expression));
            }
        }
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Note = "Review candidates, not an exhaustive localization proof. Includes direct UI properties, common message/status/tooltip calls; excludes translation key arguments. Custom painting and custom factories still require review.",
            Candidates = candidates
        }, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine($"{candidates.Count} hard-coded UI candidates inventoried: {output}");
    }

    private static bool HasLiteralText(ExpressionSyntax expression) =>
        expression.DescendantNodesAndSelf().OfType<InterpolatedStringTextSyntax>()
            .Any(text => text.TextToken.ValueText.Any(char.IsLetter)) ||
        expression.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>().Any(literal => literal.IsKind(SyntaxKind.StringLiteralExpression) &&
            literal.Token.ValueText.Any(char.IsLetter) &&
            !literal.Ancestors().TakeWhile(node => node != expression.Parent).OfType<InvocationExpressionSyntax>()
                .Any(call => call.Expression.ToString().Split('.').Last() is "T" or "VT" or "I18n"));
}
