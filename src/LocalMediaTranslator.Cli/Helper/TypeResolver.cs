using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace LocalMediaTranslator.Cli.Helper;

public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable {
    public object? Resolve(Type? type) {
        // GetService returns null for an unregistered type and propagates
        // constructor exceptions; either way Spectre renders the failure as the
        // opaque "Could not resolve type 'X'" and its default renderer prints
        // only that line. GetRequiredService throws for both cases, so the real
        // cause reaches Program.cs's top-level handler, which prints the chain.
        return type is null ? null : provider.GetRequiredService(type);
    }

    public void Dispose() {
        if (provider is IDisposable disposable)
            disposable.Dispose();
    }
}
