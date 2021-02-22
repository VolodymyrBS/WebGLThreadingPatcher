var SystemThreadingTimerLib = {
    $vars: {
        currentCallbackId : 0,
		callback: {}
    },

	SetCallback: function (onCallback)
	{
		vars.callback = onCallback;
		console.log("Set callback");
	},
	
	UpdateTimer: function(interval)
	{
		var id = ++vars.currentCallbackId;
		console.log("update timer");
		console.log(interval);
		setTimeout(function()
		{
			console.log("timeout function")
			if (id === vars.currentCallbackId)
				Runtime.dynCall('v', vars.callback);
		},
		interval);
	}
};

autoAddDeps(SystemThreadingTimerLib, '$vars');
mergeInto(LibraryManager.library, SystemThreadingTimerLib);