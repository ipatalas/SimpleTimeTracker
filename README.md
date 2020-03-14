# Introduction

That's a very simple "script" to be executed in **LINQPad**. It show number of hours the computer was on recently.
It uses event logs to get that information based on sleep/wake-up events.

# Installation

**Easy way**: just copy TimeTracker.linq to `<MyDocuments>\LINQPad Queries`. It will be automatically picked up by **LINQPad**.

**Advanced way**: For development purposes it's better to clone the repo to a folder and make a symbolic link to that file:
```
git clone <this_repo_url>
cd "<MyDocuments>\LINQPad Queries"
mklink TimeTracker.linq <path_to_repo>\TimeTracker.linq
```

# Roadmap

Do the same in F#. It's a bit of a playground for functional approach so F# would suit better.