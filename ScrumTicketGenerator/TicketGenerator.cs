using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Diagnostics;

namespace ScrumTicketGenerator
{
	public class TicketGenerator
	{
		public void Generate(string[] tickets, IProgress<string> progress, CancellationToken cancel)
		{
			bool firstLine = true;
			var epics = new List<Epic>();

			var mainTemplate = LoadTemplate("main");
			var storyTemplate = LoadTemplate("story");
			var taskTemplate = LoadTemplate("task");
			var output = "";

			foreach (var ticket in tickets)
			{
				try
				{
					if (String.IsNullOrEmpty(ticket))
						break;

					if (firstLine)
						firstLine = false;
					else
						progress.Report("\n");

					cancel.ThrowIfCancellationRequested();
					progress.Report(String.Format("[{0}] Processing story... ", ticket));
					var story = ProcessStory(ticket);

					cancel.ThrowIfCancellationRequested();
					progress.Report("Processing epic... ");
					var epic = ProcessEpic(story.EpicId, epics);

					cancel.ThrowIfCancellationRequested();
					progress.Report(String.Format("Processing sub tasks... ", ticket));
					var subTasks = ProcessSubTasks(story.SubTaskIds);

					//Write this story to the output
					string taskHtml = "";

					foreach (var subTask in subTasks)
						taskHtml += String.Format(String.Copy(taskTemplate), subTask.Name, subTask.SubTaskType);

					output += String.Format(String.Copy(storyTemplate), story.Name, epic.Counter, epic.Name, taskHtml);

					progress.Report("Done.");
				}
				catch (OperationCanceledException)
				{
					progress.Report("\n\nCancelled!");
					break;
				}
				catch (Exception ex)
				{
					progress.Report(String.Format("\n\nError: {0}\n{1}\n", ex.Message, ex.StackTrace));
				}
			}

			//Save the generated HTML
			var outputPath = ConfigurationSettings.AppSettings["outputPath"];
			if (String.IsNullOrEmpty(outputPath))
				outputPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

			var fileName = Path.Combine(outputPath, String.Format("tickets_{0}.html", DateTime.Now.ToString("yyyyMMddHHmmss")));
			using (StreamWriter writer = new StreamWriter(fileName))
			{
				writer.WriteLine(String.Format(mainTemplate, output));
			}

			progress.Report("\n\nAll tickets generated.");

			//Open the new HTML file in a browser
			try
			{
				ProcessStartInfo startInfo = new ProcessStartInfo(ConfigurationSettings.AppSettings["defaultBrowser"], fileName);
				Process.Start(startInfo);
			}
			catch (Exception ex)
			{
				progress.Report(String.Format("\n\nError opening browser: {0}\n{1}\n", ex.Message, ex.StackTrace));
			}
		}

		protected Story ProcessStory(string id)
		{
			var doc = GetTaskInfo(id);

			var story = new Story();
			story.Id = id;
			story.Name = doc.SelectSingleNode("/Task/Name").InnerText;
			story.EpicId = doc.SelectSingleNode("/Task/Epic").InnerText;

			story.SubTaskIds = new List<string>();
			var tasks = doc.SelectNodes("/Task/SubTasks/Id");

			foreach (XmlNode task in tasks)
			{
				story.SubTaskIds.Add(task.InnerText);
			}

			return story;
		}

		protected Epic ProcessEpic(string id, List<Epic> epics)
		{
			var epic = epics.Where(x => x.Id == id).FirstOrDefault();

			if (epic == null)
			{
				var doc = GetTaskInfo(id);
				var epicName = doc.SelectSingleNode("/Task/Name").InnerText;

				epic = new Epic() { Id = id, Name = epicName };
				epics.Add(epic);
			}

			return epic;
		}

		protected List<SubTask> ProcessSubTasks(List<string> ids)
		{
			var subTasks = new List<SubTask>(ids.Count);

			foreach (var id in ids)
			{
				var doc = GetTaskInfo(id);

				var subTask = new SubTask();
				subTask.Id = id;
				subTask.Name = doc.SelectSingleNode("/Task/Name").InnerText;
				subTask.SubTaskType = doc.SelectSingleNode("/Task/Type").InnerText;
				subTask.Estimate = doc.SelectSingleNode("/Task/Estimate").InnerText;

				subTasks.Add(subTask);
			}

			return subTasks;
		}

		/// <summary>
		/// Get the information about a given JIRA task
		/// </summary>
		/// <param name="ticket">The unique identifier of the JIRA task</param>
		/// <returns></returns>
		protected XmlDocument GetTaskInfo(string ticket)
		{
			var storyUrl = String.Format(ConfigurationSettings.AppSettings["taskUrl"], ticket);
			string authInfo = String.Format("{0}:{1}", ConfigurationSettings.AppSettings["username"], ConfigurationSettings.AppSettings["password"]);

			var request = WebRequest.Create(storyUrl);
			authInfo = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(authInfo));
			request.Headers["Authorization"] = "Basic " + authInfo;

			var doc = new XmlDocument();
			doc.Load(request.GetResponse().GetResponseStream());

			return doc;
		}

		/// <summary>
		/// Loads an included HTML template
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		protected string LoadTemplate(string name)
		{
			var assembly = Assembly.GetExecutingAssembly();
			var resourceName = String.Format("ScrumTicketGenerator.templates.{0}.html", name);

			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			using (StreamReader reader = new StreamReader(stream))
			{
				return reader.ReadToEnd();
			}
		}

		public class Epic
		{
			public string Id;
			public string Name;
			public int Counter;

			private static int counter = 0;

			public Epic()
			{
				Counter = counter;
				counter++;
			}
		}

		public class Story
		{
			public string Id;
			public string Name;
			public string LeadBa;
			public string LeadTester;

			public string EpicId;
			public List<string> SubTaskIds;
		}

		public class SubTask
		{
			public string Id;
			public string Name;
			public string SubTaskType;
			public string Estimate;
		}
	}
}
