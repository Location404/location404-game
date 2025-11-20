using LiteBus.Commands.Extensions.MicrosoftDependencyInjection;
using LiteBus.Messaging.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Location404.Game.Application.Features.Matchmaking.Commands.JoinMatchmakingCommand;
using Location404.Game.Application.Features.GameRounds.Commands.StartRoundCommand;
using Location404.Game.Application.Features.GameRounds.Commands.SubmitGuessCommand;
using Location404.Game.Application.Features.GameRounds.Commands.EndRoundCommand;

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
                module.RegisterFromAssembly(typeof(StartRoundCommandHandler).Assembly);
                module.RegisterFromAssembly(typeof(SubmitGuessCommandHandler).Assembly);
                module.RegisterFromAssembly(typeof(EndRoundCommandHandler).Assembly);
            });
        });

        return services;
    }
}
