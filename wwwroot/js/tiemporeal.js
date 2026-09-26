(function () {
    "use strict";

    var cfgEl = document.getElementById("tr-config");
    var panel = document.getElementById("panel");

    if (!cfgEl || !panel) return;

    var urlSocket = cfgEl.dataset.ws;
    var urlPanel = cfgEl.dataset.panel;
    var estadoEl = document.getElementById("tr-estado");
    var textoEl = document.getElementById("tr-texto");
    var toastEl = document.getElementById("tr-toast");

    var socket = null;
    var intentos = 0;
    var aviso = null;

    function pintarEstado(estado, mensaje) {
        if (estadoEl) estadoEl.dataset.estado = estado;
        if (textoEl) textoEl.textContent = mensaje;
    }

    function avisar(mensaje) {
        if (!toastEl) return;
        toastEl.textContent = mensaje;
        toastEl.hidden = false;
        requestAnimationFrame(function () { toastEl.classList.add("toast--visible"); });

        clearTimeout(aviso);
        aviso = setTimeout(function () {
            toastEl.classList.remove("toast--visible");
            setTimeout(function () { toastEl.hidden = true; }, 250);
        }, 4500);
    }

    function refrescar() {
        var lista = document.getElementById("lista");
        var vacio = document.getElementById("panel-vacio");
        var tarjetas = lista ? lista.querySelectorAll(".incidencia") : [];

        var abiertas = 0;
        var criticas = 0;
        var altas = 0;

        for (var i = 0; i < tarjetas.length; i++) {
            abiertas++;
            if (tarjetas[i].classList.contains("incidencia--p3")) criticas++;
            else if (tarjetas[i].classList.contains("incidencia--p2")) altas++;
        }

        var valores = panel.querySelectorAll(".stats .stat__value");
        if (valores.length === 3) {
            valores[0].textContent = abiertas;
            valores[1].textContent = criticas;
            valores[2].textContent = altas;
        }

        var sinLista = abiertas === 0;
        if (lista) lista.classList.toggle("is-oculto", sinLista);
        if (vacio) vacio.classList.toggle("is-oculto", !sinLista);
    }

    function sincronizar() {
        if (!urlPanel) return Promise.resolve();

        return fetch(urlPanel, { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } })
            .then(function (respuesta) {
                if (!respuesta.ok) throw new Error("HTTP " + respuesta.status);
                return respuesta.text();
            })
            .then(function (html) {
                panel.innerHTML = html;
                return true;
            })
            .catch(function (error) {
                console.warn("No se pudo sincronizar el panel:", error);
                return false;
            });
    }

    function aplicar(evento, datos) {
        if (evento !== "IncidenciaActualizada" || !datos) return;

        var id = datos.Id !== undefined ? datos.Id : datos.id;
        var estado = datos.Estado !== undefined ? datos.Estado : datos.estado;

        if (estado === "Cerrada") {
            var tarjeta = panel.querySelector('.incidencia[data-id="' + id + '"]');
            if (tarjeta) {
                tarjeta.classList.add("incidencia--saliendo");
                setTimeout(function () {
                    if (tarjeta.parentNode) tarjeta.parentNode.removeChild(tarjeta);
                    refrescar();
                }, 220);
            } else {
                sincronizar();
            }

            avisar("Incidencia #" + String(id).padStart(3, "0") + " cerrada en otra sesión");
        } else {
            sincronizar().then(function () {
                avisar("Incidencia #" + String(id).padStart(3, "0") + " actualizada: " + estado);
            });
        }
    }

    function conectar() {
        if (!urlSocket) {
            pintarEstado("error", "Sin canal configurado");
            return;
        }

        pintarEstado("conectando", intentos === 0
            ? "Conectando con PieHost…"
            : "Reconectando con PieHost… (intento " + intentos + ")");

        try {
            socket = new WebSocket(urlSocket);
        } catch (e) {
            programarReconexion();
            return;
        }

        socket.addEventListener("open", function () {
            intentos = 0;
            pintarEstado("conectado", "Tiempo real conectado");
            // al reconectar, consultar el estado vigente
            sincronizar();
        });

        socket.addEventListener("message", function (evento) {
            var mensaje;
            try {
                mensaje = JSON.parse(evento.data);
            } catch (e) {
                return;
            }

            if (!mensaje || typeof mensaje.event !== "string") return;
            if (mensaje.event.indexOf("system::") === 0) return;

            aplicar(mensaje.event, mensaje.data);
        });

        socket.addEventListener("close", function () {
            pintarEstado("desconectado", "Sin conexión en tiempo real");
            programarReconexion();
        });

        socket.addEventListener("error", function () {
            pintarEstado("error", "Error de conexión con PieHost");
        });
    }

    function programarReconexion() {
        intentos++;
        var espera = Math.min(1500 * Math.pow(1.6, intentos - 1), 15000);
        setTimeout(conectar, espera);
    }

    document.addEventListener("submit", function (evento) {
        var formulario = evento.target.closest ? evento.target.closest("form") : null;
        if (!formulario || !formulario.classList.contains("incidencia__accion")) return;

        var boton = formulario.querySelector('button[type="submit"]');
        if (boton) {
            boton.disabled = true;
            boton.textContent = "Cerrando…";
        }
    });

    conectar();
})();
