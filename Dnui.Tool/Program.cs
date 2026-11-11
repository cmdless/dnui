// The dnui tool forwards to the lockstep runtime and nothing else (constitution: Tool).
return await Dnui.Hosting.RuntimeLauncher.PassthroughAsync(args, runtimeOptions: null, CancellationToken.None);
