using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Utility;

namespace Content.Server.Afk;

[AdminCommand(AdminFlags.VarEdit)]
public sealed partial class SetAfkConfirmSoundCommand : LocalizedEntityCommands
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "setafkconfirmationsound";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("cmd-setafkconfirmationsound-invalid-arguments"));
            return;
        }

        var path = new ResPath(args[0]);
        if (!path.IsRooted)
        {
            shell.WriteError(Loc.GetString("cmd-setafkconfirmationsound-not-rooted"));
            return;
        }

        _cfg.SetCVar(CCVars.AfkConfirmSound, path.ToString());
        shell.WriteLine(Loc.GetString("cmd-setafkconfirmationsound-success", ("path", path.ToString())));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHint(Loc.GetString("cmd-setafkconfirmationsound-hint"))
            : CompletionResult.Empty;
    }
}
