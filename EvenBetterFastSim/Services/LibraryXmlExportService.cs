using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Enums;

namespace EvenBetterFastSim.Services;

public class LibraryXmlExportService
{
    public void Save(string path, ObservableCollection<SecsGemTransaction> library)
    {
        var doc = new XmlDocument();
        doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));

        var libraryNode = doc.CreateElement("Library");
        doc.AppendChild(libraryNode);

        Append(doc, libraryNode, "Name", "unknown");
        Append(doc, libraryNode, "Description", "Default library");

        foreach (var transaction in library)
            libraryNode.AppendChild(TransactionNode(doc, transaction));

        doc.Save(path);
    }

    private static XmlElement TransactionNode(XmlDocument doc, SecsGemTransaction t)
    {
        var node = doc.CreateElement("Transaction");
        Append(doc, node, "Name", t.Name);
        Append(doc, node, "Description", t.Description);
        Append(doc, node, "Stream", t.PrimaryMessage.Stream.ToString());
        Append(doc, node, "Function", t.PrimaryMessage.Function.ToString());
        Append(doc, node, "ReplyExpected", t.PrimaryMessage.Reply ? "true" : "false");
        node.AppendChild(MessageNode(doc, t.PrimaryMessage, "Primary"));
        if (t.PrimaryMessage.Reply)
            node.AppendChild(MessageNode(doc, t.ReplyMessage, "Secondary"));
        return node;
    }

    private static XmlElement MessageNode(XmlDocument doc, SecsGemDataMessage m, string tag)
    {
        var node = doc.CreateElement(tag);
        node.SetAttribute("name", m.Name);
        node.SetAttribute("desc", m.Description);
        foreach (var child in m.Children.OfType<SecsGemItem>())
            node.AppendChild(ItemNode(doc, child));
        return node;
    }

    private static XmlElement ItemNode(XmlDocument doc, SecsGemItem item)
    {
        var node = doc.CreateElement("Item");
        Append(doc, node, "Name", item.Name);
        Append(doc, node, "Description", item.Description);
        Append(doc, node, "Format", item.FormatType.ToString());
        Append(doc, node, "NLB", "0");

        if (item.FormatType == SecsGemItemFormatType.List)
        {
            foreach (var child in item.Children.OfType<SecsGemItem>())
                node.AppendChild(ItemNode(doc, child));
        }
        else
        {
            foreach (var value in item.GetStringValues())
                Append(doc, node, "Value", value);
        }

        return node;
    }

    private static void Append(XmlDocument doc, XmlElement parent, string tag, string value)
    {
        var el = doc.CreateElement(tag);
        el.InnerText = value;
        parent.AppendChild(el);
    }
}
