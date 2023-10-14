using ChatWithWeb.Data;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel;

namespace ChatWithWeb.Services
{
    public class QAService
    {
        public string SkillName { get; set; } = "QASkill";
        public string FunctionName { set; get; } = "QA";
        int MaxTokens { set; get; }
        double Temperature { set; get; }
        double TopP { set; get; }

        Dictionary<string, ISKFunction> ListFunctions = new Dictionary<string, ISKFunction>();

        IKernel kernel { set; get; }

        public QAService()
        {
            // Configure AI backend used by the kernel
            var (model, apiKey, orgId) = AppConstants.GetSettings();

            kernel = new KernelBuilder()
       .WithOpenAITextCompletionService(modelId: "gpt-3.5-turbo-instruct", apiKey: apiKey, orgId: orgId, serviceId: "davinci")
       .Build();

            SetupSkill();
        }

        public void SetupSkill(int MaxTokens = 2500, double Temperature = 0.2, double TopP = 0.5)
        {
            this.MaxTokens = MaxTokens;
            this.Temperature = Temperature;
            this.TopP = TopP;

            string skPrompt = """
Nama kamu adalah Soleha, kamu adalah asisten digital yang bisa menjawab segala macam pertanyaan dengan baik, ramah, menyenangkan dan lucu.

Q:{{$input}}
A:
""";

            var promptConfig = new PromptTemplateConfig()
            {
                Completion = new OpenAIRequestSettings() { MaxTokens = MaxTokens, Temperature = Temperature, TopP = TopP }
            };

            var promptTemplate = new PromptTemplate(
    skPrompt,                        // Prompt template defined in natural language
    promptConfig,                    // Prompt configuration
    kernel                           // SK instance
    );


            var functionConfig = new SemanticFunctionConfig(promptConfig, promptTemplate);

            var QAFunction = kernel.RegisterSemanticFunction(SkillName, FunctionName, functionConfig);
            ListFunctions.Add(FunctionName, QAFunction);
        }

        public async Task<string> Ask(string input)
        {
            string Result = string.Empty;

            try
            {
                //TokenHelper.CheckMaxToken(this.MaxTokens, input);
                var QA = await kernel.RunAsync(input, ListFunctions[FunctionName]);
                Result = QA.GetValue<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return "INFO NOT FOUND";
            }
            finally
            {

            }
            return Result;
        }

    }
}
