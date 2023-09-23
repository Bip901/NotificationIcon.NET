/*
MIT License

Copyright (c) 2023 Ori Almagor
Copyright (c) 2017 Serge Zaitsev

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

#include <gtk/gtk.h>
#include <libappindicator/app-indicator.h>

#define EXPORT __attribute__((visibility("default")))
#define TRAY_APPINDICATOR_ID "tray-id"

static AppIndicator* indicator = NULL;
static int loop_result = 0;

static void _tray_menu_cb(GtkMenuItem* item, gpointer data) {
	(void)item;
	struct tray_menu* m = (struct tray_menu*)data;
	m->cb(m);
}

static GtkMenuShell* _tray_menu(struct tray_menu* m) {
	GtkMenuShell* menu = (GtkMenuShell*)gtk_menu_new();
	for (; m != NULL && m->text != NULL; m++) {
		GtkWidget* item;
		if (strcmp(m->text, "-") == 0) {
			item = gtk_separator_menu_item_new();
		}
		else {
			if (m->submenu != NULL) {
				item = gtk_menu_item_new_with_label(m->text);
				gtk_menu_item_set_submenu(GTK_MENU_ITEM(item),
					GTK_WIDGET(_tray_menu(m->submenu)));
			}
			else if (m->checked == -1) {
				item = gtk_menu_item_new_with_label(m->text);
			}
			else {
				item = gtk_check_menu_item_new_with_label(m->text);
				gtk_check_menu_item_set_active(GTK_CHECK_MENU_ITEM(item), !!m->checked);
			}
			gtk_widget_set_sensitive(item, !m->disabled);
			if (m->cb != NULL) {
				g_signal_connect(item, "activate", G_CALLBACK(_tray_menu_cb), m);
			}
		}
		gtk_widget_show(item);
		gtk_menu_shell_append(menu, item);
	}
	return menu;
}

EXPORT void tray_update(struct tray* tray) {
	app_indicator_set_icon(indicator, tray->icon);
	// GTK is all about reference counting, so previous menu should be destroyed here
	app_indicator_set_menu(indicator, GTK_MENU(_tray_menu(tray->menu)));
}

EXPORT int tray_init(struct tray* tray) {
	if (gtk_init_check(0, NULL) == FALSE) {
		return -1;
	}
	indicator = app_indicator_new(TRAY_APPINDICATOR_ID, tray->icon,
		APP_INDICATOR_CATEGORY_APPLICATION_STATUS);
	app_indicator_set_status(indicator, APP_INDICATOR_STATUS_ACTIVE);
	tray_update(tray);
	return 0;
}

EXPORT int tray_loop(int blocking) {
	gtk_main_iteration_do(blocking);
	return loop_result;
}

EXPORT void tray_exit() { loop_result = -1; }
