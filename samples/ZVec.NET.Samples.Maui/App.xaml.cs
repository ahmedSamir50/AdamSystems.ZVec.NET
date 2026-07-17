namespace ZVec.NET.Samples.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage()) { Title = "ZVec.NET.Samples.Maui" };
	}
}
