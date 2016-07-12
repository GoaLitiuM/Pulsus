using System;
using Eto.Forms;

namespace Pulsus
{
	public class SettingsWindow : IDisposable
	{
		public class SettingsDialog : Dialog
		{
			public bool resultOk;
			public SettingsDialog(bool inGame)
			{
				Title = Program.name + " Settings";
				ShowInTaskbar = true;

				//Size = new Eto.Drawing.Size(640, 320);
				Topmost = true;

				TableLayout layout = new TableLayout()
				{
					Padding = new Eto.Drawing.Padding(5),
				};

				layout.Rows.Add(new TableRow
				(
					new Panel()
					{
						Content = new TabControl()
						{
							Pages =
							{
								new TabPage()
								{
									Text = "tab1",
									Content = new Button()
									{
										Text = "tabbutton1",
									},
									//Content
								},

								new TabPage()
								{
									Text = "tab2",
									//Content
								},
							},
						},
						Size = new Eto.Drawing.Size(640, 320)
					}
				));
				
				DynamicLayout buttonLayout = new DynamicLayout()
				{
					Padding = new Eto.Drawing.Padding(5),
					Spacing = new Eto.Drawing.Size(5, 5),
					
				};

				buttonLayout.BeginHorizontal();
				
				buttonLayout.Add(new Label { Text = Program.versionDisplay.ToString()});
				buttonLayout.Add(null);
				buttonLayout.Add(AddButton(inGame ? "OK" : "Start", OnOkClick));
				buttonLayout.Add(AddButton(inGame ? "Cancel" : "Exit", OnCancelClick));
				buttonLayout.Add(AddButton("Apply", OnApplyClick));
				buttonLayout.EndHorizontal();

				layout.Rows.Add(buttonLayout);
				//layout.SetRowScale(layout.Rows.Count-1, false);
				
				Content = layout;
			}

			private Button AddButton(string text, EventHandler<EventArgs> onClickEvent = null)
			{
				Button button = new Button()
				{
					Text = text,
				};
				if (onClickEvent != null)
					button.Click += onClickEvent;

				return button;
			}

			private void OnOkClick(object sender, EventArgs e)
			{
				resultOk = true;
				Close();
			}

			private void OnCancelClick(object sender, EventArgs e)
			{
				resultOk = false;
				Close();
			}

			private void OnApplyClick(object sender, EventArgs e)
			{

			}
		}

		Settings persistent;
		Settings settings;
		SettingsDialog dialog;

		public SettingsWindow(bool inGame)
		{
			persistent = SettingsManager.instance;
			settings = SettingsManager.Clone(persistent);

			Program.EtoInvoke(() =>
			{
				dialog = new SettingsDialog(inGame);
			});
		}

		public void Dispose()
		{
			Program.EtoInvoke(() =>
			{
				dialog.Dispose();
				dialog = null;
			});
		}

		public void ShowAsync()
		{
			Program.EtoInvoke(() =>
			{
				dialog.ShowModalAsync(Program.etoApplication.MainForm);
			});
		}

		public bool Show()
		{
			bool resultOk = false;
			Program.EtoInvoke(() =>
			{
				dialog.ShowModal(Program.etoApplication.MainForm);
				resultOk = dialog.resultOk;
			});
			return resultOk;
		}
	}
}
