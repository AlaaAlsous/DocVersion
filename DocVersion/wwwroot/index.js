"Use strict";
const loginModal = document.getElementById("loginModal");
const modalUserName = document.getElementById("modalUserName");
const modalPassword = document.getElementById("modalPassword");
const modalSubmit = document.getElementById("modalSubmit");

async function login() {
  const username = modalUserName.value.trim();
  const password = modalPassword.value.trim();

  if (!username || !password) {
    alert("Username and password are required");
    return;
  }
  try {
    const response = await fetch("/api/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        username: username,
        password: password,
      }),
    });

    if (!response.ok) {
      return;
    }
  } catch (error) {
    console.error("Login error:", error);
    return;
  }
  const data = await response.json();
  localStorage.setItem("jwt", data.token);

  loginModal.style.display = "none";
}

modalSubmit.addEventListener("click", login);

modalUserName.addEventListener("keydown", (event) => {
  if (event.key === "Enter") login();
});

modalPassword.addEventListener("keydown", (event) => {
  if (event.key === "Enter") login();
});
