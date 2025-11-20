using LiteBus.Commands.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Location404.Game.Application.Common.Result;

namespace Location404.Game.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGameApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<
            ICommandHandler<
                Application.Features.Matchmaking.Commands.JoinMatchmakingCommand.JoinMatchmakingCommand,
                Result<Application.Features.Matchmaking.Commands.JoinMatchmakingCommand.JoinMatchmakingCommandResponse>>,
            Application.Features.Matchmaking.Commands.JoinMatchmakingCommand.JoinMatchmakingCommandHandler>();

        services.AddScoped<
            ICommandHandler<
                Application.Features.GameRounds.Commands.StartRoundCommand.StartRoundCommand,
                Result<Application.Features.GameRounds.Commands.StartRoundCommand.StartRoundCommandResponse>>,
            Application.Features.GameRounds.Commands.StartRoundCommand.StartRoundCommandHandler>();

        services.AddScoped<
            ICommandHandler<
                Application.Features.GameRounds.Commands.SubmitGuessCommand.SubmitGuessCommand,
                Result<Application.Features.GameRounds.Commands.SubmitGuessCommand.SubmitGuessCommandResponse>>,
            Application.Features.GameRounds.Commands.SubmitGuessCommand.SubmitGuessCommandHandler>();

        services.AddScoped<
            ICommandHandler<
                Application.Features.GameRounds.Commands.EndRoundCommand.EndRoundCommand,
                Result<Application.Features.GameRounds.Commands.EndRoundCommand.EndRoundCommandResponse>>,
            Application.Features.GameRounds.Commands.EndRoundCommand.EndRoundCommandHandler>();

        return services;
    }
}
