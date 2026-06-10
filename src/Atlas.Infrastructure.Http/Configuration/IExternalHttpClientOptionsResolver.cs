namespace Atlas.Infrastructure.Http.Configuration;

public interface IExternalHttpClientOptionsResolver
{
    ResolvedExternalHttpClientOptions Get(string clientName);
}
