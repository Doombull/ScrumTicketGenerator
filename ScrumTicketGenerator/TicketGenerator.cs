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

					var storyId = ticket.Trim().ToUpper();

					if (firstLine)
						firstLine = false;
					else
						progress.Report("\n");

					cancel.ThrowIfCancellationRequested();
					progress.Report(String.Format("[{0}] Processing story... ", storyId));
					var story = ProcessStory(storyId);

					cancel.ThrowIfCancellationRequested();
					progress.Report("Processing epic... ");
					var epic = ProcessEpic(story.EpicId, epics);

					cancel.ThrowIfCancellationRequested();
					progress.Report("Processing sub tasks... ");
					var subTasks = ProcessSubTasks(story.SubTaskIds);

					//Write this story to the output
					string taskHtml = "";

					if (subTasks != null && subTasks.Count > 1)
						foreach (var subTask in subTasks)
							taskHtml += String.Format(String.Copy(taskTemplate), subTask.Id, subTask.Name, subTask.SubTaskType, subTask.Estimate);

					output += String.Format(String.Copy(storyTemplate), 
						story.Id,
						story.Name,
						story.LeadBa,
						story.LeadTester,
						story.Estimate,
						epic.Color, 
						epic.Name, 
						taskHtml);

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
			if (!String.IsNullOrEmpty(output))
			{
				string fileName = null;

				try
				{
					var outputPath = ConfigurationSettings.AppSettings["outputPath"];
					if (String.IsNullOrEmpty(outputPath))
						outputPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

					fileName = Path.Combine(outputPath, String.Format("tickets_{0}.html", DateTime.Now.ToString("yyyyMMddHHmmss")));
					using (StreamWriter writer = new StreamWriter(fileName))
					{
						writer.WriteLine(String.Format(mainTemplate, output));
					}

					progress.Report("\n\nAll tickets generated.");
				}
				catch (Exception ex)
				{
					progress.Report(String.Format("\n\nError writing file: {0}\n{1}\n", ex.Message, ex.StackTrace));
				}

				//Open the new HTML file in a browser
				try
				{
					if (!String.IsNullOrEmpty(fileName))
					{
						ProcessStartInfo startInfo = new ProcessStartInfo(fileName);
						Process.Start(startInfo);
					}
				}
				catch (Exception ex)
				{
					progress.Report(String.Format("\n\nError opening browser: {0}\n{1}\n", ex.Message, ex.StackTrace));
				}
			}
		}

		protected Story ProcessStory(string id)
		{
			var doc = GetTaskInfo(id);

			var story = new Story();
			story.Id = id;
			story.Name = GetFeedValue(doc, "/rss/channel/item/summary");
			story.LeadBa = GetFeedValue(doc, "/rss/channel/item/customfields/customfield[@id='customfield_16841']/customfieldvalues/customfieldvalue");
			story.LeadTester = GetFeedValue(doc, "/rss/channel/item/customfields/customfield[@id='customfield_16745']/customfieldvalues/customfieldvalue");
			story.Estimate = GetFeedValue(doc, "/rss/channel/item/aggregatetimeremainingestimate");
			story.EpicId = GetFeedValue(doc, "/rss/channel/item/customfields/customfield[@id='customfield_13544']/customfieldvalues/customfieldvalue");

			story.SubTaskIds = new List<string>();
			var tasks = doc.SelectNodes("/rss/channel/item/subtasks/subtask");

			foreach (XmlNode task in tasks)
			{
				story.SubTaskIds.Add(task.InnerText);
			}

			return story;
		}

		protected Epic ProcessEpic(string id, List<Epic> epics)
		{
			if (String.IsNullOrEmpty(id))
				return new Epic() { Name = "No Epic" };

			var epic = epics.Where(x => x.Id == id).FirstOrDefault();

			if (epic == null)
			{
				var doc = GetTaskInfo(id);

				epic = new Epic();
				epic.Name = GetFeedValue(doc, "/rss/channel/item/summary");
				epic.Color = GetFeedValue(doc, "/rss/channel/item/customfields/customfield[@id='customfield_13547']/customfieldvalues/customfieldvalue");

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
				subTask.Name = GetFeedValue(doc, "/rss/channel/item/summary");
				subTask.SubTaskType = GetFeedValue(doc, "/rss/channel/item/type");
				subTask.Estimate = GetFeedValue(doc, "/rss/channel/item/timeestimate");

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
		/// Gets the value of a node from the given document, handling missing nodes
		/// </summary>
		/// <param name="doc"></param>
		/// <param name="path"></param>
		protected string GetFeedValue(XmlDocument doc, string path)
		{
			string value = null;

			var node = doc.SelectSingleNode(path);
			if (node != null)
				value = node.InnerText;

			return value;
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
			public string Color;
		}

		public class Story
		{
			public string Id;
			public string Name;
			public string LeadBa;
			public string LeadTester;
			public string Estimate;

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
