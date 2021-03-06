﻿// <copyright file="StackdriverTraceExporter.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Google.Api.Gax.Grpc;
using Google.Cloud.Trace.V2;
using Grpc.Core;
using OpenTelemetry.Contrib.Exporter.Stackdriver.Implementation;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Contrib.Exporter.Stackdriver
{
    /// <summary>
    /// Exports a group of spans to Stackdriver.
    /// </summary>
    public class StackdriverTraceExporter : ActivityExporter
    {
        private static readonly string StackdriverExportVersion;
        private static readonly string OpenTelemetryExporterVersion;

        private readonly Google.Api.Gax.ResourceNames.ProjectName googleCloudProjectId;
        private readonly TraceServiceSettings traceServiceSettings;

        static StackdriverTraceExporter()
        {
            try
            {
                var assemblyPackageVersion = typeof(StackdriverTraceExporter).GetTypeInfo().Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
                StackdriverExportVersion = assemblyPackageVersion;
            }
            catch (Exception)
            {
                StackdriverExportVersion = $"{Constants.PackagVersionUndefined}";
            }

            try
            {
                OpenTelemetryExporterVersion = Assembly.GetCallingAssembly().GetName().Version.ToString();
            }
            catch (Exception)
            {
                OpenTelemetryExporterVersion = $"{Constants.PackagVersionUndefined}";
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StackdriverTraceExporter"/> class.
        /// </summary>
        /// <param name="projectId">Project ID to send telemetry to.</param>
        public StackdriverTraceExporter(string projectId)
        {
            this.googleCloudProjectId = new Google.Api.Gax.ResourceNames.ProjectName(projectId);

            // Set header mutation for every outgoing API call to Stackdriver so the BE knows
            // which version of OC client is calling it as well as which version of the exporter
            var callSettings = CallSettings.FromHeaderMutation(StackdriverCallHeaderAppender);
            this.traceServiceSettings = new TraceServiceSettings();
            this.traceServiceSettings.CallSettings = callSettings;
        }

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<Activity> batchActivity)
        {
            var traceWriter = new TraceServiceClientBuilder
            {
                Settings = this.traceServiceSettings,
            }.Build();

            var batchSpansRequest = new BatchWriteSpansRequest
            {
                ProjectName = this.googleCloudProjectId,
            };

            foreach (var activity in batchActivity)
            {
                batchSpansRequest.Spans.Add(activity.ToSpan(this.googleCloudProjectId.ProjectId));
            }

            // avoid cancelling here: this is no return point: if we reached this point
            // and cancellation is requested, it's better if we try to finish sending spans rather than drop it
            traceWriter.BatchWriteSpans(batchSpansRequest);

            // TODO failures
            return ExportResult.Success;
        }

        /// <summary>
        /// Appends OpenTelemetry headers for every outgoing request to Stackdriver Backend.
        /// </summary>
        /// <param name="metadata">The metadata that is sent with every outgoing http request.</param>
        private static void StackdriverCallHeaderAppender(Metadata metadata)
        {
            metadata.Add("AGENT_LABEL_KEY", "g.co/agent");
            metadata.Add("AGENT_LABEL_VALUE_STRING", $"{OpenTelemetryExporterVersion}; stackdriver-exporter {StackdriverExportVersion}");
        }
    }
}
