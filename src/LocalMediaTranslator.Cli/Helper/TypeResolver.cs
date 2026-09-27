using Spectre.Console.Cli;

namespace LocalMediaTranslator.Cli.Helper;

public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable {
    public object? Resolve(Type? type) => type != null ? provider.GetService(type) : null;
    public void Dispose() {
        if (provider is IDisposable disposable)
            disposable.Dispose();
    }
}