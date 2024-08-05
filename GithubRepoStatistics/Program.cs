using System.Text.Json;

namespace GithubRepoStatistics
{
    class Program
    {
        private static readonly string GitHubToken = "GITHUB_TOKEN";
        private static readonly string RepoOwner = "lodash";
        private static readonly string RepoName = "lodash";
        private static readonly string BranchName = "main";
        private static readonly string Url = "https://api.github.com/graphql";
        private static readonly string Query = @$"
        {{
            repository(owner: ""{RepoOwner}"", name: ""{RepoName}"") {{
                object(expression: ""{BranchName}:"") {{
                    ... on Tree {{
                        entries {{
                            name                    
                            type
                            ... on TreeEntry {{
                                object {{
                                    ... on Blob {{
                                        text
                                    }}
                                    ... on Tree {{
                                        entries {{
                                            name                                    
                                            type
                                            ... on TreeEntry {{
                                                object {{
                                                    ... on Blob {{
                                                        text
                                                    }}
                                                    ... on Tree {{
                                                        entries {{
                                                            name                                    
                                                            type
                                                            ... on TreeEntry {{
                                                                object {{
                                                                    ... on Blob {{
                                                                        text
                                                                    }}
                                                                }}
                                                            }}
                                                        }}
                                                    }}
                                                }}
                                            }}
                                        }}
                                    }}
                                }}
                            }}
                        }}
                    }}
                }}
            }}
        }}";

        static async Task Main(string[] args)
        {
            try
            {
                var fileList = await GetFileList();

                var totalCounter = new Dictionary<char, int>();

                foreach (var filePath in fileList.Values)
                {
                    totalCounter = CountLetters(filePath, totalCounter);
                }

                var sortedCounts = totalCounter.OrderByDescending(x => x.Value);

                foreach (var (letter, count) in sortedCounts)
                {
                    Console.WriteLine($"{letter}: {count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        private static async Task<Dictionary<string, string>> GetFileList()
        {
            var jsonResponse = await RequestFiles();

            return ParseFileList(jsonResponse);
        }

        private static async Task<string> RequestFiles()
        {
            var requestBody = new { query = Query };

            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", GitHubToken);

            var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync(Url, content);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        private static Dictionary<string, string> ParseFileList(string jsonResponse)
        {
            using JsonDocument document = JsonDocument.Parse(jsonResponse);
            var root = document.RootElement;
            var entries = root.GetProperty("data")
                              .GetProperty("repository")
                              .GetProperty("object")
                              .GetProperty("entries");

            return GetFilesContent(entries);
        }

        private static Dictionary<string, string> GetFilesContent(JsonElement entries)
        {
            var jsFiles = new Dictionary<string, string>();

            foreach (var file in entries.EnumerateArray())
            {
                if (file.TryGetProperty("type", out var type) && type.GetString() == "blob")
                {
                    var filePath = file.GetProperty("name").GetString();
                    if (filePath!.EndsWith(".js") || filePath.EndsWith(".ts"))
                    {
                        jsFiles.Add(filePath, file.GetProperty("object").GetProperty("text").GetString()!);
                    }
                }
                else if (file.TryGetProperty("object", out var subTree) && subTree.TryGetProperty("entries", out var subEntries))
                {
                    jsFiles = jsFiles
                        .Concat(GetFilesContent(subEntries))
                        .GroupBy(p => p.Key)
                        .ToDictionary(p => p.Key, p => p.Last().Value);
                }
            }

            return jsFiles;
        }

        private static Dictionary<char, int> CountLetters(string text, Dictionary<char, int> counter)
        {
            var normalizedText = text.Replace("\n", "").Replace(" ", "").ToLower();

            foreach (char c in normalizedText)
            {
                if (char.IsLetter(c))
                {
                    if (counter.TryGetValue(c, out int value))
                    {
                        counter[c] = ++value;
                    }
                    else
                    {
                        counter[c] = 1;
                    }
                }
            }

            return counter;
        }
    }
}
