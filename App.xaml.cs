using System.ComponentModel;

namespace Chat;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		MainPage = new AppShell();
		MainPage.BindingContext = new PromptSocketApp();
	}
}