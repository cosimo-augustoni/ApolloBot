// ApolloBot low-fi wireframes (issue #27): tabs and page states. Vanilla JS, no dependencies.
// The navigation drawer is CSS only (checkbox), so pages stay clickable without this file.
(function () {
    "use strict";

    // Tabs: <div class="wf-tabs" role="tablist"><button class="wf-tab" role="tab" aria-controls="panel-id">
    function initTabs(tabList) {
        var tabs = Array.prototype.slice.call(tabList.querySelectorAll('[role="tab"]'));

        function select(tab) {
            tabs.forEach(function (other) {
                var selected = other === tab;
                other.setAttribute("aria-selected", selected ? "true" : "false");
                var panel = document.getElementById(other.getAttribute("aria-controls"));
                if (panel) {
                    panel.hidden = !selected;
                }
            });
        }

        tabs.forEach(function (tab) {
            tab.addEventListener("click", function () {
                select(tab);
            });
        });

        select(tabs.filter(function (tab) {
            return tab.getAttribute("aria-selected") === "true";
        })[0] || tabs[0]);
    }

    // States: the URL hash picks a state (e.g. admin-status.html#stale), falling back to
    // <body data-default-state="...">. Elements with data-states="a b" are shown only in those states.
    function applyState() {
        var body = document.body;
        var fallback = body.getAttribute("data-default-state");
        if (!fallback) {
            return;
        }

        var requested = window.location.hash.replace("#", "");
        var known = Array.prototype.map.call(document.querySelectorAll("[data-state-link]"), function (link) {
            return link.getAttribute("href").replace("#", "");
        });
        var state = known.indexOf(requested) >= 0 ? requested : fallback;

        document.querySelectorAll("[data-states]").forEach(function (element) {
            var states = element.getAttribute("data-states").split(/\s+/);
            if (states.indexOf(state) >= 0) {
                element.removeAttribute("data-state-hidden");
            } else {
                element.setAttribute("data-state-hidden", "");
            }
        });

        document.querySelectorAll("[data-state-link]").forEach(function (link) {
            link.setAttribute("aria-current", link.getAttribute("href") === "#" + state ? "true" : "false");
        });
    }

    document.querySelectorAll(".wf-tabs").forEach(initTabs);
    applyState();
    window.addEventListener("hashchange", applyState);

    // Close the mobile drawer after picking a link (same-page anchors would otherwise leave it open).
    document.querySelectorAll(".wf-drawer a").forEach(function (link) {
        link.addEventListener("click", function () {
            var toggle = document.getElementById("wf-nav");
            if (toggle) {
                toggle.checked = false;
            }
        });
    });
}());
