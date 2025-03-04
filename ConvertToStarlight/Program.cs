// Define sources and targets

using System.Diagnostics;
using System.Text.RegularExpressions;
using ConvertToStarlight;
using Tomlyn;
using Tomlyn.Syntax;
using YamlDotNet.RepresentationModel;

var sourcePathPrefix = "/Users/maartenba/Projects/Duende/docs.duendesoftware.com";
var targetPathPrefix = "/Users/maartenba/Desktop/starlight/docs/src/content/docs";
var docsFolders = new List<DocsFolder>
{
    new DocsFolder("BFF/v2/docs/content", "bff/v2"),
    new DocsFolder("BFF/v3/docs/content", "bff/v3"),
    new DocsFolder("FOSS/content", "foss"),
    new DocsFolder("IdentityServer/v5/docs/content", "identityserver/v5"),
    new DocsFolder("IdentityServer/v6/docs/content", "identityserver/v6"),
    new DocsFolder("IdentityServer/v7/docs/content", "identityserver/v7"),
};

// Copy files
foreach (var docsFolder in docsFolders)
{
    var sourceDirectoryPath = Path.Combine(sourcePathPrefix, docsFolder.SourcePath);
    var targetDirectoryPath = Path.Combine(targetPathPrefix, docsFolder.ContentPath);

    if (!Directory.Exists(targetDirectoryPath))
    {
        Directory.CreateDirectory(targetDirectoryPath);
    }

    DirectoryWalker.Walk(
        sourceDirectoryPath,
        true,
        (sourcePath, file) =>
        {
            var destinationPath = Path.Combine(targetDirectoryPath, sourcePath.Replace(sourceDirectoryPath, "").TrimStart(Path.DirectorySeparatorChar), file);

            File.Copy(Path.Combine(sourcePath, file), destinationPath, true);
        },
        (sourcePath, directory) =>
        {
            var destinationPath = Path.Combine(targetDirectoryPath, sourcePath.Replace(sourceDirectoryPath, "").TrimStart(Path.DirectorySeparatorChar), directory);

            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }
        });
}

// Make changes to file contents
foreach (var docsFolder in docsFolders)
{
    // Parse parameters file
    var siteConfigurationParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var parametersFilePath = Path.Combine(sourcePathPrefix, docsFolder.SourcePath, "..", "config.toml");
    if (!File.Exists(parametersFilePath))
    {
        parametersFilePath = Path.Combine(sourcePathPrefix, docsFolder.SourcePath, "..", "config", "_default", "hugo.toml");
    }
    if (File.Exists(parametersFilePath))
    {
        var parametersToml = Toml.Parse(File.ReadAllText(parametersFilePath), options: TomlParserOptions.ParseOnly);
        foreach (var tomlDescendant in parametersToml.Descendants())
        {
            if (tomlDescendant is KeyValueSyntax keyValueSyntax)
            {
                var key = keyValueSyntax.Key!.ToString().Trim();
                if (keyValueSyntax.Value is StringValueSyntax stringSyntax)
                {
                    siteConfigurationParameters[key] = stringSyntax.Value ?? "";
                }
                else if (keyValueSyntax.Value is DateTimeValueSyntax dateTimeSyntax)
                {
                    siteConfigurationParameters[key] = dateTimeSyntax.Value.ToString();
                }
                else if (keyValueSyntax.Value is IntegerValueSyntax integerSyntax)
                {
                    siteConfigurationParameters[key] = integerSyntax.Value.ToString();
                }
                else if (keyValueSyntax.Value is BooleanValueSyntax booleanSyntax)
                {
                    siteConfigurationParameters[key] = booleanSyntax.Value ? "true" : "false";
                }
                else if (keyValueSyntax.Value is ArraySyntax)
                {
                    // skip
                }
                else
                {
                    Debugger.Break();
                }
            }
        }
    }

    // Update content
    var contentPath = Path.Combine(targetPathPrefix, docsFolder.ContentPath);

    DirectoryWalker.Walk(
        contentPath,
        true,
        (sourcePath, file) =>
        {
            if (!file.EndsWith(".md")) return;

            var filePath = Path.Combine(contentPath, sourcePath.Replace(contentPath, "").TrimStart(Path.DirectorySeparatorChar), file);

            // 0. _index.md should be index.md
            if (Path.GetFileName(filePath) == "_index.md")
            {
                var renamedFilePath = filePath.Replace("_index.md", "index.md");
                File.Move(filePath, renamedFilePath, true);
                filePath = renamedFilePath;
            }

            // Change tracking
            var fileChanged = false;
            var fileContent = File.ReadAllText(filePath);

            // Delete redirects
            if (fileContent.Contains("type: redirect") || fileContent.Contains("type: \"redirect\""))
            {
                File.Delete(filePath);
                return;
            }

            // 1. ../images/ should be images/
            if (fileContent.Contains("../images/"))
            {
                fileContent = fileContent.Replace("../images/", "images/");
                fileChanged = true;
            }

            // 2. Try parsing front matter
            if (fileContent.StartsWith("+++"))
            {
                var frontMatterEnds = fileContent.Substring(3).IndexOf("+++", StringComparison.OrdinalIgnoreCase) + 6;
                var frontMatterTomlString = fileContent.Substring(0, frontMatterEnds);

                var frontMatterToml = Toml.Parse(frontMatterTomlString.Replace("+++", ""), filePath, TomlParserOptions.ParseOnly);
                var frontMatterYaml = new Dictionary<string, object>();
                foreach (var tomlDescendant in frontMatterToml.Descendants())
                {
                    if (tomlDescendant is KeyValueSyntax keyValueSyntax)
                    {
                        var key = keyValueSyntax.Key!.ToString().Trim();
                        if (keyValueSyntax.Value is StringValueSyntax stringSyntax)
                        {
                            frontMatterYaml[key] = stringSyntax.Value ?? "";
                        }
                        else if (keyValueSyntax.Value is DateTimeValueSyntax dateTimeSyntax)
                        {
                            frontMatterYaml[key] = dateTimeSyntax.Value.ToString();
                        }
                        else if (keyValueSyntax.Value is IntegerValueSyntax integerSyntax)
                        {
                            frontMatterYaml[key] = integerSyntax.Value.ToString();
                        }
                        else if (keyValueSyntax.Value is BooleanValueSyntax booleanSyntax)
                        {
                            frontMatterYaml[key] = booleanSyntax.Value ? "true" : "false";
                        }
                        else
                        {
                            Debugger.Break();
                        }
                    }
                }

                if (frontMatterYaml.ContainsKey("chapter"))
                {
                    frontMatterYaml.Remove("chapter");
                }

                if (frontMatterYaml.TryGetValue("weight", out var weight))
                {
                    frontMatterYaml.Remove("weight");
                }

                if (weight == null && Path.GetFileName(filePath) == "index.md")
                {
                    weight = 1;
                }

                if (weight != null)
                {
                    frontMatterYaml.Add("sidebar", new Dictionary<string, string>{ { "order", weight.ToString() }});
                }

                var frontMatterYamlString = new YamlDotNet.Serialization.Serializer().Serialize(frontMatterYaml);

                fileChanged = true;
                fileContent = "---\n" + frontMatterYamlString + "---\n" + fileContent.Substring(frontMatterEnds);
            }

            // 3. Is there leading frontmatter?
            if (fileContent.IndexOf("---", StringComparison.OrdinalIgnoreCase) > 3)
            {
                fileChanged = true;
                fileContent = "---\n" + fileContent;
            }

            // 4. Rename ```c# language
            if (fileContent.Contains("```c#", StringComparison.OrdinalIgnoreCase))
            {
                fileChanged = true;
                fileContent = fileContent.Replace("```c#", "```csharp", StringComparison.OrdinalIgnoreCase);
            }

            // 5. Handle {{ cases
            if (fileContent.Contains("{{", StringComparison.OrdinalIgnoreCase))
            {
                // {{< param parameter >}}
                fileContent = Regex.Replace(fileContent, @"\{\{< param\s+(?<param>\w+)\s+>\}\}", match =>
                {
                    var parameter = match.Groups["param"].Value;

                    return siteConfigurationParameters[parameter];
                });

                // {{< ref "/session" >}}
                fileContent = Regex.Replace(fileContent, @"\{\{<.?ref\s+""(?<path>[^""]+)""\s*>.?\}\}", match => match.Groups["path"].Value);

                // {{< ref-idsrv "/session" "title" >}}
                fileContent = Regex.Replace(fileContent, @"\{\{<.?ref-idsrv\s+""(?<path>[^""]+)""(?:\s+""(?<text>[^""]+)"")?\s*>.?\}\}", match =>
                {
                    var path = match.Groups["path"].Value.TrimStart('/');
                    var text = match.Groups["text"].Success ? match.Groups["text"].Value : null;

                    return string.IsNullOrEmpty(text)
                        ? $"/identityserver/v7/{path}"
                        : $"[{text}](/identityserver/v7/{path})";
                });

                // {{% notice note %}}
                fileContent = Regex.Replace(fileContent, @"\{\{.?% notice\s+(?<type>\w+)\s+%.?\}\}", match =>
                {
                    var type = match.Groups["type"].Success
                        ? match.Groups["type"].Value
                        : "note";

                    var starlightType = type == "note"
                        ? "note"
                        : type == "warning"
                            ? "caution"
                            : "note";

                    return $":::{starlightType}";

                });

                // {{% /notice %}}
                fileContent = fileContent.Replace("{{% /notice %}}", ":::", StringComparison.OrdinalIgnoreCase);

                // {{%children style="li" /%}}
                fileContent =  Regex.Replace(fileContent, @"\{\{%.?children(?:\s+style=""(?<style>[^""]+)"")?\s+/%.?\}\}", match =>
                {
                    var style = match.Groups["style"].Success ? match.Groups["style"].Value : null;

                    return "TODO LIST CHILDREN HERE";
                });

                // {{<mermaid align="center">}}
                fileContent = Regex.Replace(fileContent, @"\{\{<.?mermaid(?:\s+align=""(?<align>[^""]+)"")?\s*>.?\}\}", match =>
                {
                    var align = match.Groups["align"].Success ? match.Groups["align"].Value : null;

                    return $"```mermaid";

                });

                // {{< /mermaid >}}
                fileContent = fileContent.Replace("{{< /mermaid >}}", "```", StringComparison.OrdinalIgnoreCase);

                // {{< youtube "zHVmzgPUImc" >}}
                fileContent = Regex.Replace(fileContent, @"\{\{.?< youtube\s+""?(?<videoId>[^""?\s*>]+)""?\s*>.?}}", match =>
                {
                    var videoId = match.Groups["videoId"].Value;

                    // Replace with a YouTube link
                    //return $"https://www.youtube.com/watch?v={videoId}";

                    // Replace with a YouTube embed
                    return $"<iframe width=\"853\" height=\"505\" src=\"https://www.youtube.com/embed/{videoId}\" title=\"YouTube video player\" frameborder=\"0\" allow=\"accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share\" referrerpolicy=\"strict-origin-when-cross-origin\" allowfullscreen></iframe>";

                    // Replace with an embedded YouTube iframe
//                     return $"""
//                             <iframe width="560" height="315"
//                                     src="https://www.youtube.com/embed/{videoId}"
//                                     frameborder="0"
//                                     allowfullscreen></iframe>
//                             """;
                });


                fileContent = fileContent.Replace("{{http", "http", StringComparison.OrdinalIgnoreCase);
                if (fileContent.Contains("{{", StringComparison.OrdinalIgnoreCase))
                {
                    Debugger.Break();
                }

                fileChanged = true;
            }

            // Write changes
            if (fileChanged)
            {
                File.WriteAllText(filePath, fileContent);
            }
        },
        (sourcePath, directory) =>
        {
            // noop
        });
}