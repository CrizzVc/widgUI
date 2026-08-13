using System.Windows;
using System.Windows.Controls;

namespace WidgUI
{
    public interface ILayeredDesktopWidget
    {
        int LayerIndex { get; set; }
    }

    public static class WidgetLayerHelper
    {
        public static void BeginHoldPreview(Window window)
        {
            WidgetRegistry.BeginTemporaryLayerBoost(window);
        }

        public static void EndHoldPreview(Window window)
        {
            WidgetRegistry.EndTemporaryLayerBoost(window);
        }

        public static void AppendLayerMenuItems(ContextMenu menu, Window window)
        {
            MenuItem layerMenu = new MenuItem { Header = "Capa" };

            MenuItem raiseItem = new MenuItem { Header = "Subir capa" };
            raiseItem.Click += (s, e) => WidgetRegistry.RaiseWidgetLayer(window);

            MenuItem lowerItem = new MenuItem { Header = "Bajar capa" };
            lowerItem.Click += (s, e) => WidgetRegistry.LowerWidgetLayer(window);

            layerMenu.Items.Add(raiseItem);
            layerMenu.Items.Add(lowerItem);

            menu.Items.Add(new Separator());
            menu.Items.Add(layerMenu);
        }
    }
}
