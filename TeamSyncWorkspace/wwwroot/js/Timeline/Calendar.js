document.addEventListener("DOMContentLoaded", function () {
    const calendarElem = document.getElementById("calendar");
    const prevBtn = document.getElementById("prev-week");
    const nextBtn = document.getElementById("next-week");
    const taskListElem = document.getElementById("taskList");
    const taskListTitle = document.getElementById("taskListTitle");
    const workspaceId = document.querySelector("input[name='WorkspaceId']").value;

    let currentDate = dayjs().startOf("week"); // Start at current week

    async function fetchTasks(startDate, endDate) {
        try {
            let response = await fetch(`/api/tasks?workspaceId=${workspaceId}&startDate=${startDate}&endDate=${endDate}`);
            return await response.json();
        } catch (error) {
            console.error("Failed to fetch tasks:", error);
            return [];
        }
    }

    async function renderWeek() {
        calendarElem.innerHTML = "";

        let startDate = currentDate.format("YYYY-MM-DD");
        let endDate = currentDate.add(6, "day").format("YYYY-MM-DD");
        let tasks = await fetchTasks(startDate, endDate);

        updateTaskList(tasks, startDate, endDate);

        let table = document.createElement("table");
        table.classList.add("calendar-table");

        // Table Header (Weekdays)
        let thead = document.createElement("thead");
        let headerRow = document.createElement("tr");
        for (let i = 0; i < 7; i++) {
            let day = currentDate.add(i, "day");
            let th = document.createElement("th");
            th.classList.add("day-header");
            th.innerText = day.format("ddd D");
            headerRow.appendChild(th);
        }
        thead.appendChild(headerRow);
        table.appendChild(thead);

        // Task Rows
        let tbody = document.createElement("tbody");
        let taskRows = new Array(7).fill(null).map(() => new Array(7).fill(null));

        tasks.forEach(task => {
            let dayIndex = dayjs(task.dueDate).day();
            for (let i = 0; i < 7; i++) {
                if (!taskRows[i][dayIndex]) {
                    taskRows[i][dayIndex] = task;
                    break;
                }
            }
        });

        taskRows.forEach(row => {
            let tr = document.createElement("tr");
            for (let i = 0; i < 7; i++) {
                let td = document.createElement("td");
                td.classList.add("task-cell");

                if (row[i]) {
                    let taskItem = document.createElement("p");
                    taskItem.classList.add("task-item");
                    taskItem.innerText = row[i].taskDescription;
                    taskItem.dataset.taskId = row[i].taskId;
                    taskItem.dataset.taskDescription = row[i].taskDescription;
                    taskItem.dataset.dueDate = row[i].dueDate;
                    taskItem.dataset.isCompleted = row[i].isCompleted;
                    taskItem.dataset.assignedId = row[i].assignedId;
                    taskItem.onclick = () => openTaskDetails(taskItem); //  Click opens modal

                    td.appendChild(taskItem);
                }

                tr.appendChild(td);
            }
            tbody.appendChild(tr);
        });

        table.appendChild(tbody);
        calendarElem.appendChild(table);
    }

    function navigateWeek(offset) {
        currentDate = currentDate.add(offset, "week");
        renderWeek();
    }

    function updateTaskList(tasks, startDate, endDate) {
        taskListElem.innerHTML = "";

        let currentWeekStart = dayjs().startOf("week").format("YYYY-MM-DD");
        let currentWeekEnd = dayjs().endOf("week").format("YYYY-MM-DD");

        //  Update the title dynamically based on week view
        if (startDate === currentWeekStart && endDate === currentWeekEnd) {
            taskListTitle.innerText = "Tasks This Week";
        } else {
            taskListTitle.innerText = `Tasks of ${dayjs(startDate).format("MMM D")} - ${dayjs(endDate).format("MMM D")}`;
        }

        if (tasks.length === 0) {
            taskListElem.innerHTML = `<p class="has-text-grey">No tasks found for this week.</p>`;
            return;
        }

        tasks.forEach(task => {
            let li = document.createElement("li");
            li.classList.add("task-item");
            li.dataset.taskId = task.taskId;
            li.dataset.taskDescription = task.taskDescription;
            li.dataset.dueDate = task.dueDate;
            li.dataset.isCompleted = task.isCompleted;
            li.dataset.assignedId = task.assignedId !== null && task.assignedId !== undefined
                ? task.assignedId
                : "null";
            console.log("Task Data:", li.dataset); // 🔍 Debug xem có nhận được không
            li.innerHTML = `<strong>${dayjs(task.dueDate).format("ddd, MMM DD")}:</strong> ${task.taskDescription} (${task.isCompleted ? "Completed" : "Pending"})`;
            li.onclick = () => openTaskDetails(li); //  Click opens modal

            taskListElem.appendChild(li);
        });
    }

    function openTaskDetails(taskItem) {
        const modal = document.getElementById("taskDetailsModal");
        const modalTitle = document.getElementById("modalTitle");
        const taskDescInput = document.getElementById("taskDescription");
        const dueDateInput = document.getElementById("taskDueDate");
        const statusSelect = document.getElementById("taskStatus");
        const taskIdInput = document.getElementById("taskId");
        const assignedUserSelect = document.getElementById("assignedUser");

        modalTitle.innerText = `Task Details - ${taskItem.dataset.taskDescription}`;
        taskDescInput.value = taskItem.dataset.taskDescription;
        dueDateInput.value = taskItem.dataset.dueDate.split("T")[0];
        statusSelect.value = taskItem.dataset.isCompleted === "true" ? "true" : "false";
        taskIdInput.value = taskItem.dataset.taskId;
        const assignID = taskItem.dataset.assignedId;
        if (assignID) {
            console.log("Assigned ID:", assignID);

            assignedUserSelect.value = assignID;
        } else {
            assignedUserSelect.value = 0;
        }
        modal.classList.add("is-active");
    }

    renderWeek();

    prevBtn.addEventListener("click", () => navigateWeek(-1));
    nextBtn.addEventListener("click", () => navigateWeek(1));
});
