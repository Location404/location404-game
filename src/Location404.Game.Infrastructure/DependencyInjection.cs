using LiteBus.Commands.Extensions.MicrosoftDependencyInjection;
using LiteBus.Messaging.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Location404.Game.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGameApplicationServices(this IServiceCollection services)
    {
        services.AddLiteBus(liteBus =>
        {
            liteBus.AddCommandModule(module =>
            {
                module.RegisterFromAssembly(
                    typeof(Application.Features.Matchmaking.Commands.JoinMatchmakingCommand.JoinMatchmakingCommandHandler).Assembly);
            });
        });

        return services;
    }
}
