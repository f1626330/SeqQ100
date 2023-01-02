using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Sequlite.WPF.Framework
{
    public class MediaElementBehavior : DependencyObject
    {
        #region IsPlaying Property

        public static DependencyProperty IsPlayingProperty =
            DependencyProperty.RegisterAttached(
                "IsPlaying", typeof(bool), typeof(MediaElementBehavior),
                new PropertyMetadata(false, OnIsPlayingChanged));
        public static bool GetIsPlaying(DependencyObject d)
        {
            return (bool)d.GetValue(IsPlayingProperty);
        }
        public static void SetIsPlaying(DependencyObject d, bool value)
        {
            d.SetValue(IsPlayingProperty, value);
        }
        private static void OnIsPlayingChanged(
            DependencyObject obj,
            DependencyPropertyChangedEventArgs args)
        {
            MediaElement me = obj as MediaElement;
            if (me == null)
                return;

            bool isPlaying = (bool)args.NewValue;
            if (isPlaying)
            {
                me.MediaEnded += Me_MediaEnded;
                me.Play();
            }
            else
            {
                //me.Close(); 
                me.MediaEnded -= Me_MediaEnded;
                me.Stop();
            }
        }


        private static void Me_MediaEnded(object sender, RoutedEventArgs e)
        {
            //throw new NotImplementedException();
            MediaElement me = sender as MediaElement;
            if (me == null)
                return;
            if (GetIsPlaying(me))
            {
                me.Position = new TimeSpan(0, 0, 1);
                me.Play();
            }
        }

        # endregion
        public static DependencyProperty IsStopPlayingProperty =
        DependencyProperty.RegisterAttached(
                "IsStopPlaying", typeof(bool), typeof(MediaElementBehavior),
                new PropertyMetadata(false, OnIsStopPlayingChanged));
        public static bool GetIsStopPlaying(DependencyObject d)
        {
            return (bool)d.GetValue(IsPlayingProperty);
        }
        public static void SetIsStopPlaying(DependencyObject d, bool value)
        {
            d.SetValue(IsPlayingProperty, value);
        }
        private static void OnIsStopPlayingChanged(
            DependencyObject obj,
            DependencyPropertyChangedEventArgs args)
        {
            bool isStopPlaying = (bool)args.NewValue;

            if (isStopPlaying)
            {
                bool isPlaying = GetIsPlaying(obj);
                if (isPlaying)
                {
                    MediaElement me = obj as MediaElement;
                    if (me == null)
                        return;
                    if (GetIsPlaying(me))
                    {
                        me.Close();
                    }
                }
            }
        }

    }
}
