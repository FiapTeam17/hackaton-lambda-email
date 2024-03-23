using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace HackathonFiap.Lambda.Email;

public class Function
{
    private readonly JsonSerializerOptions _optionsDeserialize;
    private readonly JsonSerializerOptions _optionsSerialize;
    private readonly HttpClient _httpClient;
    
    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        _optionsSerialize = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        
        _optionsDeserialize = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _httpClient = new HttpClient();
    }


    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
    /// to respond to SQS messages.
    /// </summary>
    /// <param name="evnt">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {   
        foreach (var message in evnt.Records)
        {
            await ProcessMessageAsync(message, context);
        }
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
    {   
        context.Logger.LogInformation($"Processed message {message.Body}");
        var mailgunApiKey = Environment.GetEnvironmentVariable("MAILGUN_API_KEY") ?? string.Empty;
        var mailgunDomain = Environment.GetEnvironmentVariable("MAILGUN_DOMAIN") ?? string.Empty;
        var mailgunFrom = Environment.GetEnvironmentVariable("MAILGUN_FROM") ?? string.Empty;
        var solicitacaoEmail = JsonSerializer.Deserialize<SolicitacaoEmail>(message.Body, _optionsDeserialize);

        var mailgunUrl = $"https://api.mailgun.net/v3/{mailgunDomain}/messages";
        
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", 
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{mailgunApiKey}")));
        
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(mailgunFrom), "from");
        content.Add(new StringContent(solicitacaoEmail.To), "to");
        content.Add(new StringContent(solicitacaoEmail.Subject), "subject");
        content.Add(new StringContent(solicitacaoEmail.Html), "html");
        
        var resp =  await _httpClient.PostAsync(mailgunUrl, content);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            context.Logger.LogInformation($"Status {resp.StatusCode}, message: {body}");
            throw new Exception($"Status {resp.StatusCode}, message: {body}");
        }
    }
    
}