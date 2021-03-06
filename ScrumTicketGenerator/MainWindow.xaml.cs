﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ScrumTicketGenerator
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		CancellationTokenSource cancelSource;

		public MainWindow()
		{
			InitializeComponent();
		}

		private async void btnGenerate_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(txtUsername.Text))
			{
				ShowError("Please enter a Username", "No Username");
				return;
			};

			if (String.IsNullOrEmpty(txtPassword.Password))
			{
				ShowError("Please enter a Password", "No Password");
				return;
			};

			if (String.IsNullOrEmpty(txtTickets.Text))
			{
				ShowError("Please enter at least one JIRA ticket number", "No Ticket");
				return;
			};

			txtStatus.Text = "";
			var tickets = txtTickets.Text.Split(',');
			var username = txtUsername.Text;
			var password = txtPassword.Password;

			btnGenerate.Visibility = Visibility.Hidden;
			btnCancel.Visibility = Visibility.Visible;
			
			var progressIndicator = new Progress<string>(ReportProgress);

			cancelSource = new CancellationTokenSource();

			var generator = new TicketGenerator();
			await Task.Run(() => generator.Generate(tickets, username, password, progressIndicator, cancelSource.Token));

			btnGenerate.Visibility = Visibility.Visible;
			btnCancel.Visibility = Visibility.Hidden;
		}

		private void btnCancel_Click(object sender, RoutedEventArgs e)
		{
			cancelSource.Cancel();

			btnGenerate.Visibility = Visibility.Visible;
			btnCancel.Visibility = Visibility.Hidden;
		}

		void ReportProgress(string value)
		{
			txtStatus.AppendText(value);
			txtStatus.ScrollToEnd();
		}

		void ShowError(string message, string caption)
		{
			MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}
}
