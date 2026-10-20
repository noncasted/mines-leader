// Native editor plugin that hides the native GtkMenuBar of the Unity editor on Linux.
// The menu is shown inside the main toolbar instead (Internal.CompactMenuBar), which calls
// menubar_hider_set_hidden from the editor main thread (the GTK thread).

#include <gtk/gtk.h>

#define MAX_CLASSES 8

typedef struct
{
    GtkWidgetClass* widget_class;
    void (*original_show)(GtkWidget*);
} PatchedClass;

static PatchedClass patched[MAX_CLASSES];
static int patched_count = 0;

static void ignore_show(GtkWidget* widget)
{
}

// Unity shows the menu bar again whenever editor windows open or close. Hiding it after the show
// lets it render for a frame, so the show itself is disabled for the class: the bar never becomes visible.
static gboolean patch_class(GtkWidgetClass* widget_class)
{
    for (int i = 0; i < patched_count; i++)
    {
        if (patched[i].widget_class == widget_class)
            return TRUE;
    }

    if (patched_count == MAX_CLASSES)
        return FALSE;

    patched[patched_count].widget_class = widget_class;
    patched[patched_count].original_show = widget_class->show;
    patched_count++;
    return TRUE;
}

static void apply_classes(gboolean hidden)
{
    for (int i = 0; i < patched_count; i++)
        patched[i].widget_class->show = hidden == TRUE ? ignore_show : patched[i].original_show;
}

static gboolean collect_menu_bars(GtkWidget* widget, GList** menu_bars)
{
    if (GTK_IS_MENU_BAR(widget) == TRUE)
    {
        *menu_bars = g_list_prepend(*menu_bars, widget);
        return patch_class(GTK_WIDGET_GET_CLASS(widget));
    }

    if (GTK_IS_CONTAINER(widget) == FALSE)
        return TRUE;

    gboolean result = TRUE;
    GList* children = gtk_container_get_children(GTK_CONTAINER(widget));

    for (GList* child = children; child != NULL; child = child->next)
        result = collect_menu_bars(GTK_WIDGET(child->data), menu_bars) && result;

    g_list_free(children);
    return result;
}

// Returns the number of menu bars found, or -1 when a class could not be patched.
G_MODULE_EXPORT int menubar_hider_set_hidden(int hidden)
{
    // Classes created after this call (subclasses included) inherit the handler of GtkMenuBar.
    if (patch_class(g_type_class_ref(GTK_TYPE_MENU_BAR)) == FALSE)
        return -1;

    GList* menu_bars = NULL;
    gboolean result = TRUE;
    GList* toplevels = gtk_window_list_toplevels();

    for (GList* window = toplevels; window != NULL; window = window->next)
        result = collect_menu_bars(GTK_WIDGET(window->data), &menu_bars) && result;

    g_list_free(toplevels);

    if (result == FALSE)
    {
        g_list_free(menu_bars);
        return -1;
    }

    apply_classes(hidden != 0);
    int count = 0;

    for (GList* bar = menu_bars; bar != NULL; bar = bar->next)
    {
        if (hidden != 0)
            gtk_widget_hide(GTK_WIDGET(bar->data));
        else
            gtk_widget_show(GTK_WIDGET(bar->data));

        count++;
    }

    g_list_free(menu_bars);
    return count;
}
