using ChatWithWeb.Data;
using ChatWithWeb.Models;
using Microsoft.SemanticMemory.MemoryStorage.Qdrant;
using Microsoft.SemanticMemory;
using ChatWithWeb.Helpers;
using Microsoft.SemanticMemory.MemoryStorage.DevTools;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ChatWithWeb.Services
{
    public class RAGService
    {
        public bool isReceivingResponse { set; get; } = false;
        Memory DocMemory;
        public RAGService()
        {
            Setup();
        }

        void Setup()
        {
            var (model, apiKey, orgId) = AppConstants.GetSettings();
            var configVector = new SimpleVectorDbConfig() { StorageType = SimpleVectorDbConfig.StorageTypes.TextFile, Directory = Crawler.VectorDir };

            DocMemory = new Microsoft.SemanticMemory.MemoryClientBuilder()
            .WithSimpleVectorDb(configVector)
            .WithOpenAIDefaults(apiKey, orgId)
            .BuildServerlessClient();
        }

        public async Task<RAGItem> Chat(string _userQuestion)
        {
            try
            {
                if (isReceivingResponse) return default;
                isReceivingResponse = true;
                await Task.Delay(1);
                if (string.IsNullOrEmpty(_userQuestion))
                {
                    return default;
                }
                var answer = await DocMemory.AskAsync(_userQuestion);
                var res = answer.Result;

                var newItem = new RAGItem() { Answer = res, CreatedDate = DateTime.Now, Question = _userQuestion };
                Console.WriteLine("Sources:\n");
                //only for debug
                foreach (var x in answer.RelevantSources)
                {
                    newItem.Sources.Add(new SourceItem() { Link = x.Link, Source = x.SourceName });
                    Console.WriteLine($"  - {x.SourceName}  - {x.Link} [{x.Partitions.First().LastUpdate:D}]");
                }
                return newItem;

            }
            catch (Exception ex)
            {
                Console.WriteLine("error answer question:" + ex);

            }
            finally
            {
                isReceivingResponse = false;
            }
            return default;
        }
    }
}

