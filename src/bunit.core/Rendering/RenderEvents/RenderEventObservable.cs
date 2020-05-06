using System;
using System.Collections.Generic;

namespace Bunit.Rendering.RenderEvents
{
	/// <summary>
	/// Represents a <see cref="RenderEvent"/> <see cref="IObservable{RenderEvent}"/>.
	/// </summary>
	public class RenderEventObservable : IObservable<RenderEvent>
	{
		private readonly HashSet<IObserver<RenderEvent>> _observers = new HashSet<IObserver<RenderEvent>>();

		/// <summary>
		/// Gets the observers currently subscribed to the observable.
		/// </summary>
		protected HashSet<IObserver<RenderEvent>> Observers => _observers;

		/// <inheritdoc/>
		public virtual IDisposable Subscribe(IObserver<RenderEvent> observer)
		{
			if (!_observers.Contains(observer))
				_observers.Add(observer);
			return new Unsubscriber(this, observer);
		}

		/// <summary>
		/// Unsubscribes the <paramref name="observer"/> from this observable.
		/// </summary>
		/// <param name="observer">Observer to remove.</param>
		protected virtual void RemoveSubscription(IObserver<RenderEvent> observer)
		{
			_observers.Remove(observer);
		}

		private sealed class Unsubscriber : IDisposable
		{
			private RenderEventObservable _observable;
			private IObserver<RenderEvent> _observer;

			public Unsubscriber(RenderEventObservable observable, IObserver<RenderEvent> observer)
			{
				_observable = observable;
				_observer = observer;
			}

			public void Dispose()
			{
				_observable.RemoveSubscription(_observer);
			}
		}
	}
}
