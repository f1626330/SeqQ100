using System;
using System.Reactive;

namespace Sequlite.ALF.App
{
    public static class AppObservableSubscriber
	{
		public static IDisposable Subscribe<T>(IObservable<T> observableItem, Action<T> onNext)
		{
			IObserver<T> observer = Observer.Create<T>(onNext);
			return observableItem.Subscribe(observer);
		}

		public static IDisposable Subscribe<T>(IObservable<T> observableItem, Action<T> onNext, Action<Exception> onError)
		{
			IObserver<T> observer = Observer.Create<T>(onNext, onError);
			return observableItem.Subscribe(observer);
		}

		public static IDisposable Subscribe<T>(IObservable<T> observableItem, Action<T> onNext, Action onCompleted)
		{
			IObserver<T> observer = Observer.Create<T>(onNext, onCompleted);
			return observableItem.Subscribe(observer); 
		}

		public static IDisposable Subscribe<T>(IObservable<T> observableItem, Action<T> onNext, Action<Exception> onError, Action onCompleted)
		{
			IObserver<T>  observer = Observer.Create<T>(onNext,onError,onCompleted);
			return observableItem.Subscribe(observer);
		}


		public static void Unsubscribe(IDisposable subscriber)
		{
			subscriber?.Dispose();
		}
	}
}
