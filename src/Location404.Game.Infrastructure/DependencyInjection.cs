using LiteBus.Commands.Extensions.MicrosoftDependencyInjection;
using LiteBus.Messaging.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Location404.Game.Application.Features.Matchmaking.Commands.JoinMatchmakingCommand;

namespace Location404.Game.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGameApplicationServices(this IServiceCollection services)
    {
        services.AddLiteBus(liteBus =>
        {
            liteBus.AddCommandModule(module =>
            {
                module.RegisterFromAssembly(typeof(JoinMatchmakingCommandHandler).Assembly);
            });
        });

        return services;
    }
}
