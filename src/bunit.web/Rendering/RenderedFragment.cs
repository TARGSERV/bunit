using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AngleSharp.Diffing.Core;
using AngleSharp.Dom;
using Bunit.Diffing;
using Bunit.Rendering;
using Bunit.Rendering.RenderEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bunit
{
	/// <summary>
	/// Represents an abstract <see cref="IRenderedFragment"/> with base functionality.
	/// </summary>
	public class RenderedFragment : IRenderedFragment, IRenderEventHandler
	{
		private readonly ILogger<RenderedFragment> _logger;
		private TaskCompletionSource<int>? _nextUpdate;
		private string? _snapshotMarkup;
		private INodeList? _firstRenderNodes;
		private INodeList? _latestRenderNodes;
		private INodeList? _snapshotNodes;

		private bool disposedInRenderer = false;

		private HtmlParser HtmlParser { get; }

		/// <summary>
		/// Gets the renderer used to render the <see cref="IRenderedFragmentBase"/>.
		/// </summary>
		protected ITestRenderer Renderer { get; }

		/// <summary>
		/// Gets the first rendered markup.
		/// </summary>
		protected string FirstRenderMarkup { get; }

		/// <inheritdoc/>
		public IServiceProvider Services { get; }

		/// <inheritdoc/>
		public int ComponentId { get; }

		/// <inheritdoc/>
		public string Markup { get; private set; }

		/// <inheritdoc/>
		public INodeList Nodes
		{
			get
			{
				if (_latestRenderNodes is null)
					_latestRenderNodes = HtmlParser.Parse(Markup);
				return _latestRenderNodes;
			}
		}

		/// <inheritdoc/>
		public event Action? OnMarkupUpdated;

		public event Action? OnAfterRender;

		/// <inheritdoc/>
		public Task<int> NextRender
		{
			get
			{
				if (_nextUpdate is null)
					_nextUpdate = new TaskCompletionSource<int>();
				return _nextUpdate.Task;
			}
		}

		/// <inheritdoc/>
		public int RenderCount { get; private set; }

		/// <summary>
		/// Creates an instance of the <see cref="RenderedFragment"/> class.
		/// </summary>
		public RenderedFragment(IServiceProvider services, int componentId)
		{
			if (services is null)
				throw new ArgumentNullException(nameof(services));

			_logger = GetLogger(services);
			Services = services;
			HtmlParser = services.GetRequiredService<HtmlParser>();
			Renderer = services.GetRequiredService<ITestRenderer>();
			ComponentId = componentId;
			Markup = RetrieveLatestMarkupFromRenderer();
			FirstRenderMarkup = Markup;
			Renderer.AddRenderEventHandler(this);
			RenderCount = 1;
		}

		private ILogger<RenderedFragment> GetLogger(IServiceProvider services)
		{
			var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
			return loggerFactory.CreateLogger<RenderedFragment>();
		}

		/// <inheritdoc/>
		public void SaveSnapshot()
		{
			_snapshotNodes = null;
			_snapshotMarkup = Markup;
		}

		/// <inheritdoc/>
		public IReadOnlyList<IDiff> GetChangesSinceSnapshot()
		{
			if (_snapshotMarkup is null)
				throw new InvalidOperationException($"No snapshot exists to compare with. Call {nameof(SaveSnapshot)} to create one.");

			if (_snapshotNodes is null)
				_snapshotNodes = HtmlParser.Parse(_snapshotMarkup);

			return Nodes.CompareTo(_snapshotNodes);
		}

		/// <inheritdoc/>
		public IReadOnlyList<IDiff> GetChangesSinceFirstRender()
		{
			if (_firstRenderNodes is null)
				_firstRenderNodes = HtmlParser.Parse(FirstRenderMarkup);
			return Nodes.CompareTo(_firstRenderNodes);
		}

		private string RetrieveLatestMarkupFromRenderer() => Htmlizer.GetHtml(Renderer, ComponentId);

		Task IRenderEventHandler.Handle(RenderEvent renderEvent)
		{
			HandleComponentRender(renderEvent);
			return Task.CompletedTask;
		}

		private void HandleComponentRender(RenderEvent renderEvent)
		{
			if (renderEvent.DidComponentRender(ComponentId))
			{
				_logger.LogDebug(new EventId(1, nameof(HandleComponentRender)), $"Received a new render where component {ComponentId} did render.");

				RenderCount++;

				// First notify derived types, e.g. queried AngleSharp collections or elements
				// that the markup has changed and they should rerun their queries.
				HandleChangesToMarkup(renderEvent);


				//// Then it is safe to tell anybody waiting on updates or changes to the rendered fragment
				//// that they can redo their assertions or continue processing.
				//if (_nextUpdate is { } thisUpdate)
				//{
				//	_nextUpdate = new TaskCompletionSource<int>();
				//	thisUpdate.SetResult(RenderCount);
				//}
				OnAfterRender?.Invoke();
			}
		}

		private void HandleChangesToMarkup(RenderEvent renderEvent)
		{
			if (renderEvent.HasChangesTo(ComponentId))
			{
				_logger.LogDebug(new EventId(1, nameof(HandleChangesToMarkup)), $"Received a new render where the markup of component {ComponentId} changed.");

				Markup = RetrieveLatestMarkupFromRenderer();
				_latestRenderNodes = null;

				OnMarkupUpdated?.Invoke();
			}
			else if (renderEvent.HasDiposedComponent(ComponentId))
			{
				_logger.LogDebug(new EventId(1, nameof(HandleChangesToMarkup)), $"Received a new render where the component {ComponentId} was disposed.");
				disposedInRenderer = true; // TODO: TEST THIS
				Renderer.RemoveRenderEventHandler(this);
			}
		}
	}
}
