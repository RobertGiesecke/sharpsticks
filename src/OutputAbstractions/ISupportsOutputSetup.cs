namespace SharpSticks.OutputAbstractions;

/// Implemented by an <see cref="IOutputDeviceFactory"/> that supports a one-time, typically
/// administrator-required setup step (kernel module loading, udev rules, group membership,
/// permission tweaks, etc.). Invoked by the host when the user passes a matching
/// <c>setup &lt;subcommand&gt;</c> on the command line. The host exits after the setup
/// completes — the program does not continue into its normal runtime.
public interface ISupportsOutputSetup
{
	/// CLI subcommand name that triggers this setup (case-insensitive). Examples:
	/// <c>"uinput"</c> for the Linux uinput backend, <c>"vjoy"</c> for a future Windows
	/// vJoy installer.
	string SetupSubcommandName { get; }

	/// Run the setup. The full set of button and axis routes the host knows about is passed
	/// so the setup can validate that the factory would be able to create each declared
	/// output device after the setup completes (e.g. open /dev/uinput, run UI_DEV_CREATE
	/// for each declared output device, then UI_DEV_DESTROY). Implementations should write
	/// progress to <see cref="System.Console.Out"/>; throwing or calling
	/// <see cref="System.Environment.Exit"/> on hard failure is acceptable.
	void RunSetup(
		IReadOnlyCollection<ButtonRoute> buttonRoutes,
		IReadOnlyCollection<AxisRoute> axisRoutes,
		IReadOnlyCollection<int> macroButtonNumbers);
}
