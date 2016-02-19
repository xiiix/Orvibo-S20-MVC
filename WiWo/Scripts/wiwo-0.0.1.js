"use strict"

//sockets class
var socketManager = function () {

    var sockets = [{ subscriptionTime: null }];
    var subscriptionCheck = 30000;
    var subscriptionId = 0;

    //public
    var socketsInstance = {};

    //manages subscriptions
    socketsInstance.checkSubscriptions = function () {

        var xmlhttp = new XMLHttpRequest();
        xmlhttp.onreadystatechange = function () {

            if (xmlhttp.readyState == 4 && xmlhttp.status == 200) {

                var serverData = JSON.parse(xmlhttp.responseText);
                if (serverData.length > 0) {
                    sockets = serverData;

                    //add to DOM
                    var socketsDom = document.getElementById("sockets");
                    if (socketsDom.childElementCount == 0) {
                        for (var i = 0; i < sockets.length; i++) {
                            createWidget(i);
                        }
                    }
                    else if (socketsDom.childElementCount < sockets.length) {
                        //destroy children, there are more sockets now
                        while (socketsDom.firstChild) {
                            socketsDom.removeChild(socketsDom.firstChild);
                        }

                        //rebuild
                        for (var i = 0; i < sockets.length; i++) {
                            createWidget(i);
                        }
                    }
                }

                clearInterval(subscriptionId);
                subscriptionId = setInterval(function () { socketsInstance.checkSubscriptions(); }, subscriptionCheck);
            }
        }

        xmlhttp.open("POST", "api/UI/GetSockets?subscribedTime=" + sockets[0].subscribedTime, true);
        xmlhttp.send();
    }

    //swaps state of socket
    function switchSocket(socketId) {

        var xmlhttp = new XMLHttpRequest();

        xmlhttp.onreadystatechange = function () {

            if (xmlhttp.readyState == 4 && xmlhttp.status == 200) {

                var switched = JSON.parse(xmlhttp.responseText);

                sockets[socketId].isOn = switched;
                document.getElementById("cmn-toggle-" + socketId).checked = sockets[socketId].isOn;
            }
        }

        xmlhttp.open("POST", "api/UI/Switch", true);
        xmlhttp.setRequestHeader("Content-type", "application/json");
        xmlhttp.send(JSON.stringify(sockets[socketId]));
    }

    //creates a row widget in the DOM
    function createWidget(socketId) {

        var socketsDom = document.getElementById("sockets");

        var row = document.createElement("div");
        row.className = "row";
        row.id = "row" + socketId;

        var question = document.createElement("div");
        question.className = "question";
        question.id = "question" + socketId;
        question.innerHTML = sockets[socketId].name == "" ? "Unnamed Socket" : sockets[socketId].name;

        var toggle = document.createElement("div");
        toggle.className = "switch";
        toggle.id = "switch" + socketId;

        var control = document.createElement("input");
        control.classList.add("cmn-toggle");
        control.classList.add("cmn-toggle-round");
        control.id = "cmn-toggle-" + socketId;
        control.type = "checkbox";

        if (sockets[socketId].isOn) {
            control.checked = true;
        }
        else {
            control.checked = false;
        }

        control.addEventListener("change", function (e) {
            switchSocket(parseInt(this.id.split("cmn-toggle-")[1]));
        }, true);

        var label = document.createElement("label");
        label.htmlFor = "cmn-toggle-" + socketId;

        toggle.appendChild(control);
        toggle.appendChild(label);

        row.appendChild(question);
        row.appendChild(toggle);

        socketsDom.appendChild(row);
    }

    return socketsInstance;
}();

//init the sockets
window.onload = function () {

    socketManager.checkSubscriptions();
}