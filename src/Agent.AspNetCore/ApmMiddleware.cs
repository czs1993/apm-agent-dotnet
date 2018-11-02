﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Elastic.Agent.Core.Report;
using Elastic.Agent.Core.Model.Payload;
using Elastic.Agent.Core;

namespace Apm_Agent_DotNet.AspNetCore
{
	public class ApmMiddleware
	{
		PayloadSender payloadSender = new PayloadSender();
		private readonly RequestDelegate _next;

		public ApmMiddleware(RequestDelegate next)
		{
			_next = next;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			var sw = Stopwatch.StartNew();
			
			await _next(context);
			
			sw.Stop();

			var payload = new Payload
			{
				Service = new Service
				{
					Agent = new Agent
					{
						Name = Consts.AgentName,
						Version = Consts.AgentVersion
					},
					Name = "ASPDotNET Core Request",
					Framework = new Framework { Name = "ASP.NET Core", Version = "2.1" }, //TODO: Get version
					Language = new Language {  Name = "C#"} //TODO
				},
			
			};

			var transactions = new List<Transaction> {
					new Transaction {
						Name = $"{context.Request.Method} {context.Request.Path}",
						Duration = sw.ElapsedMilliseconds,
						Id = Guid.NewGuid(),
						Type = "request",
						Result = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 ? "success" : "failed",
						Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.FFFZ"),
						Context = new Context
						{
							Request = new Request
							{
								Method = context.Request.Method,
								Socket = new Socket{ Encrypted = context.Request.IsHttps, Remote_address = context.Connection.RemoteIpAddress.ToString()},
								Url = new Url
								{
									Full = context.Request?.Path.Value,
									HostName = context.Request.Host.Host,
									Protocol = "HTTP", //TODO
									Raw = context.Request?.Path.Value
								}
								//HttpVersion TODO
							},
							Response = new Response
							{
								Finished = context.Response.HasStarted, //TODO ?
								Status_code = context.Response.StatusCode
							}
						},
					}
				};

			if(SpanReporter.Spans.Count > 0)
			{
				transactions[0].Spans = SpanReporter.Spans;
			}

			payload.Transactions = transactions;

			await payloadSender.SendPayload(payload); //TODO: Make it background!
			SpanReporter.Spans.Clear();
		}
	}
}